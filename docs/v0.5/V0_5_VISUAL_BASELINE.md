# 第一阶段代表场景性能前测

日期：2026-09-07。第一阶段前测交付完成，结果为 **FailedBaseline**：数据覆盖有效，但性能与引擎检查失败。不是动画、稳定性或发布验收通过；不改变正式Release门禁。

## 方法与范围

- 独立诊断工程引用正式Main、Dashboard与WorldView，保留50ms实时模拟、UI刷新及后台自动保存；使用隔离临时存档，不读取玩家存档。测量固定权威战斗片段循环回放，地图生成在计时前完成，不代表完整长期推进成本。
- 两个场景各七组合短采样（预热5秒、采20秒）：迷你窗Low、标准窗Low/Medium/High、大窗Low/Medium/High。每场景追加三轮大窗High长采样（预热30秒、采120秒），共20组、约21分钟。完整42组合长采样使用工具Exhaustive模式，留至第五阶段。
- 正式迷你窗只有状态与按钮、不绘制战场；不能与历史独立WorldView窄窗实验混用。标准960×640，大窗物理1920×1280/逻辑960×640，迷你384×216；逐样本校验实际窗口尺寸及实际绘制帧数。
- Stopwatch测量逐帧间隔；记录实际绘制计数、模拟峰值、UI刷新、保存耗时、工作集、GC与按20逻辑处理器归一的进程CPU。保存是后台耗时，不等于主线程停顿；长帧记录中的最近模拟/UI/保存观测值不是该帧独占归因。
- 每场景另采30张连续画面及时间戳，与计时采样分离，避免截图开销污染性能；不是120秒全程录像。没有覆盖所有技能、极限投射物/召唤物、所有界面、两小时增长和托盘CPU。

## 固定输入与环境

| 场景 | 正式基准构筑 | 等级/地图 | 片段起点/时长 | 最大敌人数 | 事件/空间帧 |
| --- | --- | --- | --- | --- | --- |
| normal | core.benchmark.breaker.entry | 80 / T1 Safe | 8200 / 6800ms | 10 | 777 / 156 |
| dense | core.benchmark.breaker.endgame | 120 / T20 Abyss | 17000 / 11250ms | 39 | 3376 / 419 |

固定种子20260907。完整构筑与时间线哈希见[正常输入](evidence/baseline-2026-09-07/fixture-normal.json)、[高密度输入](evidence/baseline-2026-09-07/fixture-dense.json)。MapItem的AreaLevel字段表示此处地图阶级，MonsterLevel另行计算。

Windows 11家庭中文版10.0.26200，i7-12700H，20逻辑处理器；实际渲染器NVIDIA RTX3060 Laptop GPU，OpenGL3.3 Compatibility，驱动546.92；Godot4.7.2 Mono。DPI144、窗口内容缩放1；高性能电源计划，采集时电池98%、状态2。运行Debug诊断构建，不是最终Release发布包。详见[环境](evidence/baseline-2026-09-07/environment.json)、[主机](evidence/baseline-2026-09-07/host.json)、[供电](evidence/baseline-2026-09-07/power.txt)。

## 长采样结果

每轮预热30秒、采样120秒，窗口1920×1280、High。完整20组数据含短样本及CPU/内存/保存/GC见[原始统计](evidence/baseline-2026-09-07/baseline.json)。

| 场景/轮次 | 95分位ms | 99分位ms | 最大ms | 超40ms帧数 |
| --- | --- | --- | --- | --- |
| normal / 1 | 16.701 | 16.850 | 21.129 | 0 |
| normal / 2 | 16.694 | 16.823 | 26.581 | 0 |
| normal / 3 | 16.69 | 16.89 | 62.088 | 3 |
| dense / 1 | 16.694 | 16.832 | 17.598 | 0 |
| dense / 2 | 16.69 | 16.86 | 17.30 | 0 |
| dense / 3 | 16.691 | 16.806 | 18.218 | 0 |

三个长帧均位于normal第三轮：含预热的101.578202秒处62.0876ms、101.9148913秒处47.7041ms、103.6000356秒处46.7103ms。相邻采样GC计数无增量，但不足以确定或排除根因；不得直接归咎于GC、保存、音频或驱动。见[完整长帧记录](evidence/baseline-2026-09-07/long-frames.json)。

运行期间同时记录WASAPI GetBufferSize错误及输出设备失效警告；画面仍实际绘制。保留[引擎诊断](evidence/baseline-2026-09-07/engine-diagnostics.txt)，第五阶段需稳定环境复测和定位。不能用无报错的历史smoke结果替换本轮日志。

## 画面问题与冻结门槛

已查看[正常场景截帧](evidence/baseline-2026-09-07/case-04.webp)：多层大圆环重叠，并越过战场进入界面文字区域。源码缺少对应战场裁剪；几何是否与实际判定一致仍需逐事件核对。见动画审核中的scene.effect-bounds、危险几何/时限/密度及Boss技能契约问题，第四阶段统一修复。

性能目标不因失败放宽：标准/大窗60FPS，95分位≤18ms、99分位≤25ms、最大帧≤40ms、模拟峰值≤10ms、UI刷新≤16ms、工作集≤700MiB。长期增长≤80MiB与托盘CPU≤1%留待第五阶段验证。当前只有数据覆盖通过，性能及引擎检查未通过；详见[机器可读结论](evidence/baseline-2026-09-07/collection.json)。

## 可移植证据与复跑

- [逐帧数据与60张连续画面压缩包](evidence/baseline-2026-09-07/traces-and-sequences.zip)：20份逐帧JSON、60张WebP、2份序列时间戳。
- [SHA256清单](evidence/baseline-2026-09-07/checksums.json)：证据目录按原始字节保存，不做Git换行转换。
- 工具：[启动脚本](../../scripts/visual_baseline.ps1)、[归档脚本](../../scripts/collect_visual_baseline.ps1)、[说明](../../tools/VisualBaseline/README.md)。默认归档拒绝引擎错误；RecordFailedRun仅供保留已审核失败前测，不能绕过尺寸、帧数、冻结输入或完整性检查，也不是发布门禁。
- 历史隔离WorldView试验、错误构筑/窗口失效及smoke运行不混入此数据集；仓库保留当前完整代表场景数据，其他artifacts不提交。

生产玩法代码没有改动。2026-09-07重新执行完整Release门禁通过：753/753测试、44张PNG与6482个占用切片、全部经济/战斗/构筑/装备审计及Godot导入/启动检查。诊断工具14组合smoke和60张序列采集通过；最终归档脚本复跑通过，31份文件SHA256一致，压缩包82个条目。自动测试通过不等于以上性能和实机问题已解决。
