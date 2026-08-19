# Handoff — 商店 / 小吉 / 战斗奖励 / 抽卡

日期：2026-08-20  
操作者：Zf  
仓库检查点：https://github.com/LingYzh/mikusb-snow-local  
分支：`checkpoint/shop-rewards`（提交 `7770a27` + 本 handoff）  
**不要 push 到** `origin`（`MikuLeaks/MikuSB`）。远程名 `checkpoint` 才是这份私有检查点。

## 现状（读完这段就能接着干）

登录已经能进游戏。用户要修的四件事**在玩法层都写了代码，但实测没生效**。根因已经确认：服务端启动时相关 Excel **整表加载失败（0 条）**，后面的 CallGS 等于空跑。

最后一次启动报错链：

1. `ShowAward` / `ShopId` / `ShopType` 与 C# 属性重名 → Newtonsoft 拒绝反序列化  
2. 修好重名后，`chapter/level.json` 的 `PlayerExp` 出现 `""` / null，`int` 无法转换 → 关卡表仍是 0 条  

`PlayerExp` 已改成 `JToken` + `[JsonIgnore]`，DLL 已拷到 `D:\Snow\Server`（`MikuCommon.dll` 约 2026-08-20 01:14）。**用户尚未用这次构建做过一次干净的加载确认。**

下一步不是继续加玩法，而是：**先证明表加载成功**，再按日志对回包格式。

## 环境

| 项 | 路径 / 命令 |
|---|---|
| 源码 | `D:\Snow\MikuSB` |
| 运行时 | `D:\Snow\Server` |
| 游戏 | `D:\Snow\data\game\Game\Binaries\Win64\Game.exe` |
| 启动顺序 | `MikuSB.exe` 等到「启动完成!」→ `SnowLauncher.exe`（不要直接开 `Game.exe`） |
| 日志 | `D:\Snow\Server\Config\Logs\Server.log` |
| 发布 | `dotnet publish D:\Snow\MikuSB\MikuSB\MikuSB.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false --property:PublishDir=D:\Snow\Server\_next\` 然后拷 `MikuCommon.dll` `MikuGameServer.dll` `MikuSB.dll` `MikuSB.exe` `SdkServer.dll` 到 `D:\Snow\Server` |
| Hosts / 端口 | 管理员跑 `D:\Snow\绑定官方登录IP到本地.bat`；443 和 18443 → 13443；hosts **一行一个主机名** |
| 补丁 | `D:\Snow\Server\Patch\MikuSB-Patch.dll`（v4.3：不要把 ws2_32 ordinal 30 当 WSAConnect，那是 `GetAddrInfoExW`） |

**不要做：**

- 不要启动 `E:\SnowO`（完整官服备份）——会和当前 hosts / AppData / patch / SOCKS 冲突  
- 不要把网盘包拷到 D 盘根目录；`E:\BaiduNetdiskDownload\尘白禁区` 是同一套 MikuSB，同样缺 `ShopLogic_GetGoodsList`，卡池日期同样过期。「必须放 D 盘」只是他们启动器写死路径  
- 不要在开发时跑 `3-抓取官方数据包`（占用 18888）  
- 不要编译不完整的本地 `hde64.c`；TCP login wrapper 的 seq 保持 **0**  
- 不要在 `/query` JSON 上重复 `type`/`Type`（会 HTTP 500）

## 四个玩法问题

### 1. 常规商店没商品（含主角宿舍服饰）

- 客户端狂调 `ShopLogic_GetGoodsList`，旧服务端无 handler → `sErr` → Lua 约 1 秒重试、无超时 → 空店 + 转圈  
- 商品在 `D:\Snow\Server\Resources\shop\goods.json`，页签在 `shop/shop_tab.json`  
- 宿舍服饰：shop id **50–53**  
- 已实现：`ShopLogic_GetGoodsList` / `RefreshGoods` / `BuyGoods`（及 Buy / BuyItem / ExchangeGoods 别名）  
- 购买次数记在 attr **gid 27**（付费商城 IB 用 gid 26，不要混）  
- 回包目前是 `{ "goodsId": buyCount, ... }`，没有额外字符串 key（避免 `pairs()` 把表当购买次数）  
- `GetOpenTime` 回 `nBegin`/`nEnd` 为 `yyyyMMddHHmm` 数字，不是 unix

### 2. 宿舍小吉商店购买卡加载

- UI：小吉优选 `GiftExchange` / 小吉精选 `GiftExchange2`  
- 配置：`D:\Snow\Server\Resources\house\gift_exchange.json`  
- 打开商店同样会打 `GetGoodsList`；购买可能是 `ShopLogic_Buy*` 或 `House_Request` / `GiveGiftToArea`  
- 已实现 `GiveGiftToArea`（及 GiftExchange / BuyGift / ExchangeGift / GiveGift 别名）：扣 `NeedItems`/`NeedMoney`，发 `Gift`  
- 未知 `ShopLogic_*` 现在回 `{}` 而不是 `sErr`，用来打断无限转圈（客户端没有超时）  
- `House_Request` 未知 FuncName 也会 synthesize 成功，不再静默不回包

### 3. 所有战斗不给奖励

- `Chapter_DealLevelSettlement` 以前对 `Chapter_LevelSettlement` 回 **空数组**  
- 现 `StageSettlement.GrantAsync`：发 `ShowAward` + `BaseDrop`/`RandomDrop` 骰子；首通用 **gid 201**（不要用 gid 22，bootstrap 已经把所有关标成通关）  
- 主线有奖励的例子：level **10101**（1-1）。ID 9012/10/11 等测试图本身 `ShowAward` 就是空的  
- 结算 `tbParam`：章节是奖励数组；序章 `Chapter_NewPrologueSettlement` 包在 `tbShowAward` 里  
- 其它模式（爬塔结算本身仍基本空奖励；爬塔领奖接口会发货）先不扩

### 4. 抽卡只看到一个池（最显眼、也最不像纯服务端能修完）

- `gacha/gacha.json` 共 192 池。按客户端日期，2026-08-20 只有 ID **1/2/3** 仍在有效期（常驻角色/武器，UI 看起来像「一个池」）  
- 限时 Type 2 全在 2024–2025 过期  
- 服务端 `D:\Snow\Server\Resources\gacha\gacha.json` 的 `PoolTime` 结束时间已全部改成 **203512310400**（此文件**不在** git 源码仓内）  
- **卡池列表是客户端 PAK 筛的**，只改服务端 JSON 通常不会让 UI 多出限时池  
- `Gacha_Launch` / `Gacha_UpSelect` 已能抽；额外加了 `Gacha_GetPoolList` 等猜测 API（日志里从未出现过 `Gacha_*` 缺 handler）  
- HTTP `GET /ob202307/webfile/mainland/banner/config/gm-gm.json` 以前 404（ASP.NET `MapFallback` 不匹配带扩展名路径）。已加 `WebfileController` + catch-all。这是登录页 banner，**不是**游戏内卡池表

## 已改的关键文件（源码仓内）

表加载：

- `Common/Data/Excel/ShopGoodsExcel.cs` `ShopTabExcel.cs` `GiftExchangeExcel.cs`
- `Common/Data/Excel/ChapterLevelExcel.cs` `DailyLevelExcel.cs` `RoleLevelExcel.cs`
- `Common/Data/Excel/DropGroupExcel.cs` `JsonTokenLists.cs`
- `Common/Data/GameData.cs`

玩法：

- `GameServer/Server/CallGS/Handlers/Shop/ShopLogic_GetGoodsList.cs`（含 Buy）
- `GameServer/Server/CallGS/Handlers/Shop/ShopLogic_GetOpenTime.cs`
- `GameServer/Game/Reward/RewardGrant.cs`
- `GameServer/Server/CallGS/Handlers/Chapter/StageSettlement.cs`
- `GameServer/Server/CallGS/Handlers/Chapter/Chapter_DealLevelSettlement.cs`
- `GameServer/Server/CallGS/Handlers/House/House_Func/HouseGift.cs`
- `GameServer/Server/CallGS/Handlers/House/House_Request.cs`
- `GameServer/Server/CallGS/Handlers/Gacha/Gacha_GetPoolList.cs`
- `GameServer/Server/CallGS/CallGSRouter.cs`（Shop/Gacha/House/结算打 param；Shop 未知 API 回 `{}`）
- `SdkServer/Handlers/WebfileController.cs` `SdkServer/SdkServer.cs`

物品约定：GDPLN = `[Genre, Detail, Particular, Level, Count]`。货币 gid **1**，sid = `moneyId * 2 + 1`（金币 moneyId 1 → sid 3）。Bootstrap 货币 999_999_999。

## 建议的下一步（按顺序，不要跳）

1. **加载门禁**  
   关游戏和 `MikuSB.exe`，再开服务端。日志必须非 0：
   - `ShopGoodsExcel`（约 489）
   - `ShopTabExcel`（约 16）
   - `ChapterLevelExcel`（远大于 0）
   - `GiftExchangeExcel`（远大于 0）  
   仍有 `读取 xxx 失败` → 先修表，不要测商店。  
   同时确认 `D:\Snow\Server\MikuCommon.dll` 时间戳新于本次启动。

2. **ResourceManager 单行容错**（强烈建议在继续玩法前做）  
   `Common/Data/ResourceManager.cs` 现在一行坏字段整张表作废。改成跳过坏行继续加载，并打出该行 `ID`。

3. **表加载成功后再测，用 CallGS 日志对格式**  
   Router 已对 `ShopLogic_*` / `Gacha_*` / `House_Request` / `Chapter_DealLevelSettlement` 打 `param=`。  
   - 商店有 `GetGoodsList shopId=N count=M` 但 UI 仍空 → 回包形状不对，按真实 param 改  
   - 小吉购买仍转圈 → 看实际 API 名，补 handler，响应 `Api` 字段必须和请求同名  
   - 战斗无奖励 → 看 `Settlement level=` 和 `sCmd`；确认不是空奖励测试关

4. **抽卡**  
   服务端日期已放开。要 UI 显示多个限时池，需要改客户端表（`PAK_Game_Script_0` / `PreloadConfig` 是压缩的，明文搜不到 `ShopLogic_GetGoodsList`）。候选：做高优先级 `_P.pak` 覆盖，或找热更 `version_require.json` 是否能推表。不要指望 `gm-gm.json`。

## 已知协议猜测（Lua 未还原）

`GetGoodsList` 请求：`{nShopId}` / `nShopID` / `nId`  
成功：goodsId → buyCount；`sErr` 会重试。

`BuyGoods`：`{nShopId, nGoodsId, nCount}` → 扣 Price1（4–5 维 GDPLN）或 `[moneyId, amount]`，发 GDPLN。

结算：`{"sCmd":"Chapter_LevelSettlement","tbParam":{nId, bWin?}}`  
章节 `tbParam` 就是奖励数组；序章用 `tbShowAward`。

## 网盘参考包

`E:\BaiduNetdiskDownload\尘白禁区`：同样是 MikuSB，`MikuGameServer.dll` 更大但字符串里同样没有 `ShopLogic_GetGoodsList`。只读参考，不要在当前 hosts/patch 环境下运行。
