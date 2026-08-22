using Google.Protobuf;
using MikuSB.Data;
using MikuSB.Data.Excel;
using MikuSB.Database;
using MikuSB.Database.Account;
using MikuSB.Database.Inventory;
using MikuSB.Database.Player;
using MikuSB.Enums.Item;
using MikuSB.GameServer.Command;
using MikuSB.GameServer.Game.Character;
using MikuSB.GameServer.Game.Inventory;
using MikuSB.GameServer.Game.Lineup;
using MikuSB.GameServer.Game.Support;
using MikuSB.GameServer.Game.Weapon;
using MikuSB.GameServer.Server;
using MikuSB.Proto;
using MikuSB.TcpSharp;
using MikuSB.Util;
using MikuSB.Util.Extensions;

namespace MikuSB.GameServer.Game.Player;

public class PlayerInstance(PlayerGameData data)
{
    #region Property
    public Connection? Connection { get; set; }

    public static readonly List<PlayerInstance> _playerInstances = [];
    public int Uid { get; set; }
    public bool Initialized { get; set; }
    public bool IsNewPlayer { get; set; }

    #endregion

    #region Data & Manager

    public PlayerGameData Data { get; set; } = data;
    public CharacterManager CharacterManager { get; set; } = null!;
    public InventoryManager InventoryManager { get; set; } = null!;
    public LineupManager LineupManager { get; set; } = null!;

    #endregion

    #region Initializers
    public PlayerInstance(int uid) : this(new PlayerGameData { Uid = uid })
    {
        // new player
        IsNewPlayer = true;
        Data.Name = PlayerGameData.NormalizeDisplayName(AccountData.GetAccountByUid(uid)?.Username);

        DatabaseHelper.CreateInstance(Data);

        var t = Task.Run(async () =>
        {
            await InitialPlayerManager();
            foreach (var skinCard in GameData.CardSkinData.Values)
            {
                await InventoryManager.AddSkinItem((ItemTypeEnum)skinCard.Genre, skinCard.Detail, skinCard.Particular, skinCard.Level, false);
            }
            foreach (var ar in GameData.ArItemData.Values)
            {
                await InventoryManager.AddArItem((ItemTypeEnum)ar.Genre, ar.Detail, ar.Particular, ar.Level, false);
            }
            foreach (var manifest in GameData.ManifestationData.Values)
            {
                await InventoryManager.AddManifestationItem((ItemTypeEnum)manifest.Genre, manifest.Detail, manifest.Particular, manifest.Level, false);
            }
            foreach (var card in GameData.CardData.Values)
            {
                await CharacterManager.AddCharacter((ItemTypeEnum)card.Genre, card.Detail, card.Particular, card.Level, sendPacket:false);
            }
            var selected = CharacterManager.CharacterData.Characters
                .OrderBy(_ => Guid.NewGuid())
                .Take(3)
                .Select(x => x.Guid)
                .ToList();

            await LineupManager.UpdateLineup(1, selected[0], selected[1], selected[2],false);

        });
        t.Wait();

        Initialized = true;
    }
    private async ValueTask InitialPlayerManager()
    {
        Uid = Data.Uid;
        Data.LastActiveTime = Extensions.GetUnixSec();
        InventoryManager = new InventoryManager(this);
        LineupManager = new LineupManager(this);
        CharacterManager = new CharacterManager(this);

        await Task.CompletedTask;
    }
    public T InitializeDatabase<T>() where T : BaseDatabaseDataHelper, new()
    {
        var instance = DatabaseHelper.GetInstanceOrCreateNew<T>(Uid);
        return instance!;
    }

    #endregion

    #region Network
    public async ValueTask OnEnterGame()
    {
        if (!Initialized) await InitialPlayerManager();
        Data.EnsureDisplayName();
        await CharacterManager.RepairCharacterWeapons();
        await RemoveGeneratedInventoryItems();
        await GrantSafeTestResources();
        await GrantOfflineEnergyCube();
        await GrantOfflineResourceCapsAndWeapons();
        await GrantRequestedInventoryTopUp();
        await GrantEligibleWeaponParts();
        await GrantNonRewardWeaponParts();
        await GrantAllWeaponSkins();
        await GrantPresetSupportCards();
        await GrantRequestedMaterialTopUp();
        await RebuildPresetSupportCards();
        await UnlockOfflineCollection();
    }

    public IEnumerable<BaseGameItemInfo> GetSupplyItems() =>
        InventoryManager.InventoryData.Items.Values.Where(x => (x.TemplateId & 0xFFFF) == 5);

    // Older offline builds populated every historical supply template at a
    // GM-sized count.  A number of those templates deliberately have no local
    // icon/description, and their presence makes the client reject all item
    // usage as containing an unavailable item.  Remove that generated batch
    // exactly once, while preserving normally earned low-count supplies.
    private const uint GeneratedInventoryCleanupGid = 250;
    private const uint GeneratedInventoryCleanupSid = 1;
    private const uint SafeTestResourceGrantSid = 2;
    private const uint EnergyCubeGrantSid = 3;
    private const uint CollectionUnlockSid = 4;
    private const uint ResourceCapsAndWeaponsGrantSid = 5;
    private const uint RequestedInventoryTopUpSid = 6;
    private const uint NonRewardWeaponPartGrantSid = 7;
    private const uint WeaponSkinGrantSid = 8;
    private const uint PresetSupportCardGrantSid = 9;
    private const uint RequestedMaterialTopUpSid = 10;
    private const uint SupportCardRebuildSid = 12;
    private const uint CollectionRikiGroupId = 103;
    private const uint CollectionRikiUnlockMask = (1u << 30) - 1;

    private static readonly (uint Detail, uint Particular, uint Level, uint Count)[] SafeSupplyGrants =
    [
        // Character, weapon and support EXP materials.
        (1, 1, 1, 5000), (1, 1, 2, 2000), (1, 1, 3, 1000), (1, 1, 4, 500),
        (2, 1, 1, 5000), (2, 1, 2, 2000), (2, 1, 3, 1000), (2, 1, 4, 500),
        (3, 1, 1, 5000), (3, 1, 2, 2000), (3, 1, 3, 1000), (3, 1, 4, 500),

        // Common weapon and support upgrade materials with confirmed local
        // icons.  Do not add the historical templates flagged Close=1.
        (4, 1, 1, 1000), (4, 2, 1, 1000), (4, 3, 1, 1000), (4, 4, 1, 1000),
        (5, 1, 1, 1000), (5, 1, 2, 600), (5, 1, 3, 300), (5, 1, 4, 150),
        (6, 1, 2, 600), (6, 2, 1, 600), (6, 3, 1, 600),
        (7, 1, 1, 1000), (7, 1, 2, 600)
    ];

    private static readonly (uint Detail, uint Particular, uint Level, uint Count)[] SafeContainerGrants =
    [
        // Small, locally-present gift boxes plus energy boxes to exercise the
        // two item-use paths without flooding the bag.
        (4, 1, 1, 5), (4, 1, 2, 5), (4, 1, 3, 5), (5, 1, 3, 3),
        (2, 1, 3, 5), (2, 1, 4, 5), (6, 1, 1, 3)
    ];

    private async ValueTask RemoveGeneratedInventoryItems()
    {
        if (Data.Attrs.Any(x => x.Gid == GeneratedInventoryCleanupGid &&
                                x.Sid == GeneratedInventoryCleanupSid && x.Val > 0))
            return;

        var generatedSupplyIds = InventoryManager.InventoryData.Items
            .Where(x => x.Value.ItemType == ItemTypeEnum.TYPE_SUPPLIES && x.Value.ItemCount >= 90000)
            .Select(x => x.Key)
            .ToList();
        var orphanedItemIds = InventoryManager.InventoryData.Items
            .Where(x => x.Value.ItemType == ItemTypeEnum.TYPE_USEABLE &&
                        !GameData.OtherItemData.ContainsKey(x.Value.TemplateId))
            .Select(x => x.Key)
            .ToList();

        foreach (var id in generatedSupplyIds.Concat(orphanedItemIds))
            InventoryManager.InventoryData.Items.Remove(id);

        Data.Attrs.Add(new PlayerAttr
        {
            Gid = GeneratedInventoryCleanupGid,
            Sid = GeneratedInventoryCleanupSid,
            Val = 1
        });
        DatabaseHelper.SaveDatabaseType(InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(Data);
        await Task.CompletedTask;
    }

    private async ValueTask GrantSafeTestResources()
    {
        if (Data.Attrs.Any(x => x.Gid == GeneratedInventoryCleanupGid &&
                                x.Sid == SafeTestResourceGrantSid && x.Val > 0))
            return;

        foreach (var grant in SafeSupplyGrants)
        {
            var templateId = GameResourceTemplateId.FromGdpl(
                (uint)ItemTypeEnum.TYPE_SUPPLIES, grant.Detail, grant.Particular, grant.Level);
            if (GameData.SuppliesData.TryGetValue(templateId, out var supply))
                await InventoryManager.AddSuppliesItem(supply, grant.Count, false);
        }

        foreach (var grant in SafeContainerGrants)
        {
            var templateId = GameResourceTemplateId.FromGdpl(
                (uint)ItemTypeEnum.TYPE_USEABLE, grant.Detail, grant.Particular, grant.Level);
            if (GameData.OtherItemData.TryGetValue(templateId, out var container) &&
                !container.IsClosed && container.Param1 > 0)
                await InventoryManager.AddOtherItem(container, grant.Count, false);
        }

        Data.Attrs.Add(new PlayerAttr
        {
            Gid = GeneratedInventoryCleanupGid,
            Sid = SafeTestResourceGrantSid,
            Val = 1
        });
        DatabaseHelper.SaveDatabaseType(InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(Data);
    }

    private async ValueTask GrantOfflineEnergyCube()
    {
        if (Data.Attrs.Any(x => x.Gid == GeneratedInventoryCleanupGid &&
                                x.Sid == EnergyCubeGrantSid && x.Val > 0))
            return;

        var templateId = GameResourceTemplateId.FromGdpl(
            (uint)ItemTypeEnum.TYPE_SUPPLIES, 13, 2, 3);
        if (GameData.SuppliesData.TryGetValue(templateId, out var energyCube))
        {
            var owned = InventoryManager.InventoryData.Items.Values
                .FirstOrDefault(x => x.TemplateId == templateId);
            if (owned == null)
                await InventoryManager.AddSuppliesItem(energyCube, 500, false);
            else if (owned.ItemCount < 500)
                owned.ItemCount = 500;
        }

        Data.Attrs.Add(new PlayerAttr
        {
            Gid = GeneratedInventoryCleanupGid,
            Sid = EnergyCubeGrantSid,
            Val = 1
        });
        DatabaseHelper.SaveDatabaseType(InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(Data);
    }

    private async ValueTask GrantOfflineResourceCapsAndWeapons()
    {
        if (Data.Attrs.Any(x => x.Gid == GeneratedInventoryCleanupGid &&
                                x.Sid == ResourceCapsAndWeaponsGrantSid && x.Val > 0))
            return;

        // The normal item cap is deliberately well below the global stack
        // maximum, while still being enough to test all relevant exchanges.
        await EnsureSupplyCount(13, 2, 3, 9_999); // 能量方晶
        await EnsureSupplyCount(28, 1, 1, 9_999); // 萤星卡券

        // Closed rows are obsolete/unavailable templates; Particular=255 is
        // the generic fallback weapon for each class rather than a usable
        // collectible.  The remaining rows are all visible, equippable weapon
        // templates with a local icon and skill definition.
        foreach (var weapon in GameData.WeaponData.Values.Where(x =>
                     !x.IsClosed && x.Particular != 255 && x.Icon != 0 &&
                     x.Class > 0 && x.Color is >= 3 and <= 5 &&
                     x.DefaultSkillID.Count > 0))
        {
            var templateId = GameResourceTemplateId.FromGdpl(
                weapon.Genre, weapon.Detail, weapon.Particular, weapon.Level);
            if (InventoryManager.GetWeaponItemByTemplateId(templateId) == null)
            {
                await InventoryManager.AddWeaponItem(
                    ItemTypeEnum.TYPE_WEAPON,
                    weapon.Detail,
                    weapon.Particular,
                    weapon.Level,
                    weaponLevel: 1,
                    sendPacket: false);
            }
        }

        Data.Attrs.Add(new PlayerAttr
        {
            Gid = GeneratedInventoryCleanupGid,
            Sid = ResourceCapsAndWeaponsGrantSid,
            Val = 1
        });
        DatabaseHelper.SaveDatabaseType(InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(Data);
    }

    private async ValueTask EnsureSupplyCount(uint detail, uint particular, uint level, uint minimum)
    {
        var templateId = GameResourceTemplateId.FromGdpl(
            (uint)ItemTypeEnum.TYPE_SUPPLIES, detail, particular, level);
        if (!GameData.SuppliesData.TryGetValue(templateId, out var supply))
            return;

        var owned = InventoryManager.InventoryData.Items.Values
            .FirstOrDefault(x => x.TemplateId == templateId);
        if (owned == null)
        {
            await InventoryManager.AddSuppliesItem(supply, minimum, false);
        }
        else if (owned.ItemCount < minimum)
        {
            owned.ItemCount = minimum;
        }
    }

    private async ValueTask GrantRequestedInventoryTopUp()
    {
        if (Data.Attrs.Any(x => x.Gid == GeneratedInventoryCleanupGid &&
                                x.Sid == RequestedInventoryTopUpSid && x.Val > 0))
            return;

        // Full stacks of the three standard upgrade-material families are
        // enough to test character, weapon, and logistics enhancement without
        // adding unrelated event-only experience resources.
        foreach (var supply in GameData.AllSuppliesData.Where(x =>
                     x.Genre == (uint)ItemTypeEnum.TYPE_SUPPLIES &&
                     x.Detail is 1 or 2 or 3 && x.ProvideExp > 0))
        {
            await EnsureSupplyCount(supply.Detail, supply.Particular, supply.Level, 99_999);
        }

        // All locally defined character extracts use Detail=4.  Their client
        // stack limit is 999,999, unlike ordinary supplies.
        foreach (var extract in GameData.AllSuppliesData.Where(x =>
                     x.Genre == (uint)ItemTypeEnum.TYPE_SUPPLIES && x.Detail == 4))
        {
            await EnsureSupplyCount(extract.Detail, extract.Particular, extract.Level, 999_999);
        }

        await EnsureSupplyCount(13, 2, 1, 90_000); // 武器组件
        await EnsureSupplyCount(26, 1, 4, 50);     // 虹膜录入证明

        Data.Attrs.Add(new PlayerAttr
        {
            Gid = GeneratedInventoryCleanupGid,
            Sid = RequestedInventoryTopUpSid,
            Val = 1
        });
        DatabaseHelper.SaveDatabaseType(InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(Data);
    }

    private async ValueTask GrantRequestedMaterialTopUp()
    {
        if (Data.Attrs.Any(x => x.Gid == GeneratedInventoryCleanupGid &&
                                x.Sid == RequestedMaterialTopUpSid && x.Val > 0))
            return;

        await EnsureSupplyCount(9, 1, 2, 99_999); // 光纤轴突
        await EnsureSupplyCount(9, 1, 3, 99_999); // 量子胞体

        Data.Attrs.Add(new PlayerAttr
        {
            Gid = GeneratedInventoryCleanupGid,
            Sid = RequestedMaterialTopUpSid,
            Val = 1
        });
        DatabaseHelper.SaveDatabaseType(InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(Data);
    }

    private async ValueTask GrantEligibleWeaponParts()
    {
        var grants = await WeaponPartUnlockService.GrantForEligibleWeaponsAsync(this);
        if (grants.Count > 0)
            DatabaseHelper.SaveDatabaseType(InventoryManager.InventoryData);
    }

    private async ValueTask GrantNonRewardWeaponParts()
    {
        if (Data.Attrs.Any(x => x.Gid == GeneratedInventoryCleanupGid &&
                                x.Sid == NonRewardWeaponPartGrantSid && x.Val > 0))
            return;

        var levelUnlockParts = WeaponPartUnlockService.GetLevelUnlockPartTemplateIds();
        foreach (var part in GameData.WeaponPartsData.Values)
        {
            var templateId = GameResourceTemplateId.FromGdpl(
                part.Genre, part.Detail, part.Particular, part.Level);
            if (levelUnlockParts.Contains(templateId))
                continue;

            await InventoryManager.AddWeaponPartItem(
                ItemTypeEnum.TYPE_WEAPON_PART,
                part.Detail,
                part.Particular,
                part.Level,
                sendPacket: false);
        }

        Data.Attrs.Add(new PlayerAttr
        {
            Gid = GeneratedInventoryCleanupGid,
            Sid = NonRewardWeaponPartGrantSid,
            Val = 1
        });
        DatabaseHelper.SaveDatabaseType(InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(Data);
    }

    private async ValueTask GrantAllWeaponSkins()
    {
        if (Data.Attrs.Any(x => x.Gid == GeneratedInventoryCleanupGid &&
                                x.Sid == WeaponSkinGrantSid && x.Val > 0))
            return;

        foreach (var skin in GameData.WeaponSkinData.Values)
        {
            await InventoryManager.AddWeaponSkinItem(
                ItemTypeEnum.TYPE_WEAPON_SKIN,
                skin.Detail,
                skin.Particular,
                skin.Level,
                sendPacket: false);
        }

        Data.Attrs.Add(new PlayerAttr
        {
            Gid = GeneratedInventoryCleanupGid,
            Sid = WeaponSkinGrantSid,
            Val = 1
        });
        DatabaseHelper.SaveDatabaseType(InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(Data);
    }

    private async ValueTask GrantPresetSupportCards()
    {
        if (Data.Attrs.Any(x => x.Gid == GeneratedInventoryCleanupGid &&
                                x.Sid == PresetSupportCardGrantSid && x.Val > 0))
            return;

        foreach (var template in GameData.SupportCardData.Where(IsOfflineEligibleSupportCard))
        {
            var card = await InventoryManager.AddSupportCardItem(
                template.Detail,
                template.Particular,
                template.Level,
                template.MaxLevel,
                sendPacket: false);
            if (card != null)
                SupportAffixPresetService.ApplyBestLegalAffixes(card, template);
        }

        Data.Attrs.Add(new PlayerAttr
        {
            Gid = GeneratedInventoryCleanupGid,
            Sid = PresetSupportCardGrantSid,
            Val = 1
        });
        DatabaseHelper.SaveDatabaseType(InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(Data);
    }

    private async ValueTask RebuildPresetSupportCards()
    {
        if (Data.Attrs.Any(x => x.Gid == GeneratedInventoryCleanupGid &&
                                x.Sid == SupportCardRebuildSid && x.Val > 0))
            return;

        // Reissue the offline test set instead of trying to mutate its old
        // random rows.  This removes previous placeholder/closed entries and
        // guarantees each retained card receives the current preset.
        InventoryManager.InventoryData.SupportCards.Clear();
        foreach (var template in GameData.SupportCardData.Where(IsOfflineEligibleSupportCard))
        {
            var card = await InventoryManager.AddSupportCardItem(
                template.Detail,
                template.Particular,
                template.Level,
                template.MaxLevel,
                sendPacket: false);
            if (card != null)
                SupportAffixPresetService.ApplyBestLegalAffixes(card, template);
        }

        Data.Attrs.Add(new PlayerAttr
        {
            Gid = GeneratedInventoryCleanupGid,
            Sid = SupportCardRebuildSid,
            Val = 1
        });
        DatabaseHelper.SaveDatabaseType(InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(Data);
        await Task.CompletedTask;
    }

    private static bool IsOfflineEligibleSupportCard(SupportCardExcel template) =>
        !template.IsClosed && template.Icon != 0 && !string.IsNullOrWhiteSpace(template.I18n);

    private async ValueTask UnlockOfflineCollection()
    {
        if (Data.Attrs.Any(x => x.Gid == GeneratedInventoryCleanupGid &&
                                x.Sid == CollectionUnlockSid && x.Val > 0))
            return;

        // The client gates the character collection in 30-bit Riki pages.
        // Mark every page unlocked so ownership and collection state cannot
        // drift apart on an offline profile.  Full Break is also required by
        // the client before it offers excess character extracts for exchange.
        for (uint sid = 1; sid <= 400; sid++)
        {
            var attr = Data.Attrs.FirstOrDefault(x => x.Gid == CollectionRikiGroupId && x.Sid == sid);
            if (attr == null)
            {
                Data.Attrs.Add(new PlayerAttr
                {
                    Gid = CollectionRikiGroupId,
                    Sid = sid,
                    Val = CollectionRikiUnlockMask
                });
            }
            else
            {
                attr.Val |= CollectionRikiUnlockMask;
            }
        }

        foreach (var character in CharacterManager.CharacterData.Characters)
            character.Break = Math.Max(character.Break, 4);

        Data.Attrs.Add(new PlayerAttr
        {
            Gid = GeneratedInventoryCleanupGid,
            Sid = CollectionUnlockSid,
            Val = 1
        });
        DatabaseHelper.SaveDatabaseType(CharacterManager.CharacterData);
        DatabaseHelper.SaveDatabaseType(Data);
        await Task.CompletedTask;
    }

    public async ValueTask OnLogin()
    {
        _playerInstances.Add(this);
        await Task.CompletedTask;
    }

    public static PlayerInstance? GetPlayerInstanceByUid(long uid)
        => _playerInstances.FirstOrDefault(player => player.Uid == uid);
    public void OnLogoutAsync()
    {
        _playerInstances.Remove(this);
    }
    public async ValueTask SendPacket(BasePacket packet)
    {
        if (Connection?.IsOnline == true) await Connection.SendPacket(packet);
    }
    public async ValueTask SendPacket(int cmdId, IMessage msg)
    {
        if (Connection?.IsOnline == true) await Connection.SendPacket(cmdId,msg);
    }

    #endregion

    #region Actions
    public async ValueTask OnHeartBeat()
    {
        DatabaseHelper.ToSaveUidList.SafeAdd(Uid);
        await Task.CompletedTask;
    }

    public async ValueTask ReceiveMessage(uint sendUid, uint recvUid, string? message = null, uint? emojiId = null)
    {
        var data = new ChatMsg
        {
            Type = ChatType.Friend,
            Sender = sendUid,
            Recver = recvUid,
            Emoji = emojiId ?? 0,
            Text = ChatMessageHelper.NormalizeForClient(message),
            Profile = Data.ToProfileProto(),
            TimeStamp = ChatMessageHelper.BuildClientTimestamp()
        };

        await SendPacket(CmdIds.NtfFriendChat, data);

        if (recvUid == ConfigManager.Config.ServerOption.ServerProfile.Uid)
        {
            if (message != null)
            {
                if (message.StartsWith("/")) message = message[1..].Trim();
                CommandExecutor.ExecuteCommand(new PlayerCommandSender(this), message);
            }
        }
    }

    #endregion

    #region Serialization

    public PlayerProfile ToServerFriendProto()
    {
        var server = ConfigManager.Config.ServerOption.ServerProfile;
        var proto = new PlayerProfile
        {
            Pid = (uint)server.Uid,
            Account = server.Name,
            Name = server.Name,
            Sex = server.Gender,
            Level = (uint)server.Level,
            Sign = server.Signature
        };
        return proto;
    }

    public Proto.Player ToPlayerProto(bool includeSupportCards = true)
    {
        BuildPlayerAttr();
        var displayName = PlayerGameData.NormalizeDisplayName(Data.Name);
        var proto = new Proto.Player
        {
            Pid = (ulong)Data.Uid,
            Account = displayName,
            Provider = displayName,
            Channel = "gm",
            Subchannel = "gm",
            Name = displayName,
            Level = Data.Level,
            Sex = Data.Gender,
            Vigor = Data.Vigor,
            Solutions = { LineupManager.LineupData.LineupInfo.Values.Select(x => x.ToProto()) },
            Badges = { InventoryManager.InventoryData.Items.Values.Where(x => x.ItemType == ItemTypeEnum.TYPE_BADGE).Select(x => (ulong)x.UniqueId) }
        };

        foreach (var chara in CharacterManager.CharacterData.Characters) proto.Items.Add(chara.ToProto());
        foreach (var item in InventoryManager.InventoryData.Items.Values) proto.Items.Add(item.ToProto());
        foreach (var skin in InventoryManager.InventoryData.Skins.Values) proto.Items.Add(skin.ToProto());
        foreach (var weapon in InventoryManager.InventoryData.Weapons.Values) proto.Items.Add(weapon.ToProto());
        if (includeSupportCards)
        {
            foreach (var card in InventoryManager.InventoryData.SupportCards.Values) proto.Items.Add(card.ToProto());
        }
        foreach (var x in Data.Attrs)
        {
            uint gid = x.Gid;
            uint sid = x.Sid;
            uint val = x.Val;

            if (gid == 0)
            {
                proto.Attrs[sid] = val;
                continue;
            }

            proto.Attrs[ToPackedAttrKey(gid, sid)] = val;   
            proto.Attrs[ToShiftedAttrKey(gid, sid)] = val;
        }

        foreach (var x in Data.StrAttrs)
        {
            proto.StrAttrs[ToShiftedAttrKey(x.Gid, x.Sid)] = x.Val;
        }

        foreach (var (key, value) in BuildMoneySync())
        {
            proto.Money[key] = value;
        }

        proto.ShowItems.AddRange(Data.ShowItems);

        return proto;
    }

    public void SetDisplayName(string? name)
    {
        Data.Name = PlayerGameData.NormalizeDisplayName(name);
    }

    public void SetShowItem(int index, ulong itemId)
    {
        if (index <= 0)
            return;

        while (Data.ShowItems.Count < index)
            Data.ShowItems.Add(0);

        Data.ShowItems[index - 1] = itemId;
    }

    public void SetStrAttr(uint gid, uint sid, string value)
    {
        var attr = Data.StrAttrs.FirstOrDefault(x => x.Gid == gid && x.Sid == sid);
        if (attr == null)
        {
            attr = new PlayerStrAttr
            {
                Gid = gid,
                Sid = sid
            };
            Data.StrAttrs.Add(attr);
        }

        attr.Val = value;
    }

    public uint ToPackedAttrKey(uint gid, uint sid)
    {
        if (gid == 0)
            return sid;

        return (gid * 10000) + sid;
    }

    public uint ToShiftedAttrKey(uint gid, uint sid)
    {
        if (gid == 0)
            return sid;

        return (gid << 16) | sid;
    }

    public Dictionary<string, int> BuildMoneySync()
    {
        var currentMoney = (int)Math.Min(int.MaxValue, GetAttrValue(1, 3));
        var sync = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["."] = currentMoney,
            ["gm.gm"] = currentMoney,
            ["jinshan.jinshan"] = currentMoney,
            ["pc_jinshan.pc_jinshan"] = currentMoney
        };
        return sync;
    }

    private uint GetAttrValue(uint gid, uint sid)
    {
        return Data.Attrs.FirstOrDefault(x => x.Gid == gid && x.Sid == sid)?.Val ?? 0;
    }

    public void BuildPlayerAttr(bool additional = false)
    {
        var bootstrapAttrs = BuildLobbyBootstrapAttrs().ToList();
        if (additional) bootstrapAttrs.AddRange(BuildGirlFurnitureAttrs());
        var existingAttrs = Data.Attrs
            .ToDictionary(x => (x.Gid, x.Sid));
        var seenAttrs = new HashSet<(uint Gid, uint Sid)>();

        foreach (var (gid, sid, value) in bootstrapAttrs)
        {
            if (!seenAttrs.Add((gid, sid)))
                continue;

            if (existingAttrs.TryGetValue((gid, sid), out var attr))
            {
                if (attr.Val < value)
                    attr.Val = value;

                continue;
            }

            var newAttr = new PlayerAttr
            {
                Gid = gid,
                Sid = sid,
                Val = value
            };

            Data.Attrs.Add(newAttr);
            existingAttrs[(gid, sid)] = newAttr;
        }
    }

    private static IEnumerable<(uint Gid, uint Sid, uint Value)> BuildGirlFurnitureAttrs()
    {
        const uint furnitureUnlockedValue = 153391689;
        var groupFurnitureByArea = new Dictionary<uint, uint>();
        foreach (var pos in GameData.HouseFurniturePosData.Values)
        {
            var areaId = pos.AreaId;
            var groupId = pos.GroupId;
            uint selectedIndex = 1;
            var shift = (groupId - 1) * 3;
            if (!groupFurnitureByArea.TryGetValue(areaId, out var packed)) packed = 0;
            packed |= (selectedIndex << (int)shift);
            groupFurnitureByArea[areaId] = packed;
        }

        for (uint girlId = 0; girlId <= 50; girlId++)
        {
            var baseSid = girlId * 50;
            for (uint offset = 10; offset <= 19; offset++)
                yield return (101, baseSid + offset, furnitureUnlockedValue);

            if (groupFurnitureByArea.TryGetValue(girlId, out var groupValue))
                yield return (101, baseSid + 20, groupValue);
        }

        // Massage room furniture
        // 10010..10019
        for (uint sid = 10010; sid <= 10019; sid++)
            yield return (101, sid, furnitureUnlockedValue);

        // Massage room group state
        yield return (101, 10020, 1);

        // Hot spring furniture
        // 15001..15010
        for (uint sid = 15001; sid <= 15010; sid++)
            yield return (101, sid, furnitureUnlockedValue);

        // Beach furniture
        // 17101..17110
        for (uint sid = 17101; sid <= 17110; sid++)
            yield return (101, sid, furnitureUnlockedValue);

        for (uint sid = 30000; sid < 31000; sid++)
            yield return (101, sid, furnitureUnlockedValue);
    }

    private static IEnumerable<(uint Gid, uint Sid, uint Value)> BuildLobbyBootstrapAttrs()
    {
        // GuideLogic uses group 4. Value 999 is safely above every configured step count,
        // so the client treats these guides as already completed.
        yield return (4, 0, 5);
        yield return (11, 1, 1);
        yield return (57, 0, 1);
        yield return (99, 3, 30);
        yield return (110, 1, 1);
        yield return (178, 1, 1_700_000_000);
        yield return (187, 1, 2);

        // Cash.GetMoneyCount uses group 1 with sid = moneyId * 2 + 1 for most currencies.
        // Fill a wide currency id range so every in-game currency starts effectively unlimited.
        for (uint moneyId = 1; moneyId <= 200; moneyId++)
            yield return (1, moneyId * 2 + 1, 999_999_999);

        for (uint guideId = 1; guideId <= 150; guideId++)
            yield return (4, guideId, 999);

        for (uint guideId = 10_000; guideId <= 10_300; guideId++)
            yield return (4, guideId, 999);

        for (uint guideId = 11_000; guideId <= 11_300; guideId++)
            yield return (4, guideId, 999);

        for (uint guideId = 12_000; guideId <= 12_100; guideId++)
            yield return (4, guideId, 999);

        for (uint guideId = 22_000; guideId <= 22_100; guideId++)
            yield return (4, guideId, 999);

        // Additional guide ids referenced directly by the Lua scripts and observed client logs.
        foreach (var guideId in new uint[] { 10_031, 10_041, 10_061, 10_081, 10_101, 10_224, 11_006, 11_202, 11_210, 22_002 })
            yield return (4, guideId, 999);

        // Launch.GPASSID = 22 stores pass counts. ChapterLevel.GID = 21 stores star flags.
        // Unlock every level defined in level.json so all chapters are accessible from the start.
        foreach (var levelId in GameData.ChapterLevelData.Keys)
        {
            yield return (21, levelId, 7);
            yield return (22, levelId, 1_700_000_000);
        }

        foreach (var levelId in GameData.DailyLevelData.Keys)
        {
            yield return (21, levelId, 7);
            yield return (22, levelId, 1_700_000_000);
        }

        // Role fragment chapters use Condition.PRE_LEVEL against Launch.GPASSID as well.
        // Mark every role level as cleared so character-specific stages beyond the first one unlock.
        foreach (var levelId in GameData.RoleLevelData.Keys)
        {
            yield return (21, levelId, 7);
            yield return (22, levelId, 1_700_000_000);
        }

        foreach (var guide in GameData.GuideData.Values)
        {
            yield return (4, guide.ID, 999);
        }

        for (uint favor = 0; favor <= 50; favor++)
            yield return (101, favor * 50, 500);

        // Main Scene 0 mean default scene
        yield return (132, 1, 0);
    }
    #endregion
}
