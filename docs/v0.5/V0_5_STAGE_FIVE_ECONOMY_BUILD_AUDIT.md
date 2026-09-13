# v0.5 第五阶段：经济与构筑平衡审计

日期：2026-09-13。第 3 项经济/构筑平衡切片已完成，审计入口为 `tools/HarborAudit`，每个构筑场景与港口难度固定种子运行 32 次，全部调用正式 `HarborRunner.Run`。

## 港口三档与构筑差异

| 难度 | 构筑场景 | 费用 | 物品等级 | 成功率 | 平均耗时(s) | 中位耗时(s) |
|---:|---|---:|---:|---:|---:|---:|
| 1 | 均衡 | 1,000 | 100 | 100.0% | 63.94 | 63.75 |
| 1 | 高机动低防御 | 1,000 | 100 | 100.0% | 47.72 | 47.88 |
| 1 | 重装低速 | 1,000 | 100 | 100.0% | 92.28 | 91.08 |
| 1 | 低输出高生存 | 1,000 | 100 | 0.0% | 300.00 | 300.00 |
| 2 | 均衡 | 3,000 | 110 | 100.0% | 77.99 | 78.70 |
| 2 | 高机动低防御 | 3,000 | 110 | 100.0% | 59.26 | 59.25 |
| 2 | 重装低速 | 3,000 | 110 | 100.0% | 107.39 | 107.50 |
| 2 | 低输出高生存 | 3,000 | 110 | 0.0% | 300.00 | 300.00 |
| 3 | 均衡 | 8,000 | 120 | 100.0% | 99.88 | 98.05 |
| 3 | 高机动低防御 | 8,000 | 120 | 100.0% | 77.97 | 76.73 |
| 3 | 重装低速 | 8,000 | 120 | 18.8% | 269.55 | 300.00 |
| 3 | 低输出高生存 | 8,000 | 120 | 0.0% | 300.00 | 300.00 |

结论：移速确实缩短实际行动时间；防御能提高生存窗口，但低输出会在行动时限内失败；第 3 档需要接近终局输出的构筑，重装路线仍是风险边界。费用、物品等级和失败扣费均来自正式配置，报告没有用评分替代战斗结果。

## 普通地图收益基线

- 100,000 次固定种子经济审计见 [Resources 经济审计](../v0.4/Resources_ECONOMY_AUDIT.md)；T1/T16/T20 地图续航为 `1.1534/1.0000/0.9018`，Boss 传奇率为 `7.959%`。
- 36 套构筑的正式输出、有效生命、恢复和场景门槛见 [Builds 审计](../v0.4/Builds_BUILD_AUDIT.md)，结构与数值门禁为 `36/36`。
- 港口首个切片的事务、失败、重载、离线和宝箱边界见 [首个验收切片](V0_5_STAGE_FIVE_FIRST_SLICE.md)。

## 可复现命令

```powershell
$env:DOTNET_BIN='C:\Users\super_wjc\.dotnet-sdk-8.0.424\dotnet.exe'
& $env:DOTNET_BIN run --project tools/HarborAudit/HarborAudit.csproj --configuration Release -- 32 artifacts/v05-harbor-balance-audit.md
& $env:DOTNET_BIN run --project tools/EconomyAudit/EconomyAudit.csproj --configuration Release --no-build -- 100000 artifacts/v05-economy-audit.md
& $env:DOTNET_BIN run --project tools/BuildsAudit/BuildsAudit.csproj --configuration Release --no-build -- artifacts/v05-builds-audit.md
```

本切片完成经济/构筑报告；42 场景长矩阵、托盘 CPU、长期内存和音频复测仍按用户指令暂缓。
