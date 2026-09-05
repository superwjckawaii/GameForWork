# Release.1 v0.2 候选版发布门禁

Release.1 不增加玩法内容，只修复候选版验证链，确保“PASS”代表可复现的无错误构建、导入、运行和打包结果。

## 修正范围

- `verify.ps1` 不再读取 PowerShell 子脚本遗留或为空的 `$LASTEXITCODE`；素材脚本抛错会直接终止门禁。
- `verify.ps1`、`stability.ps1` 与 `package-demo.ps1` 共用 .NET/Godot 路径解析和原生进程检查。
- Godot 导入、启动、稳定性运行和 Release 导出除检查退出码外，同时拒绝 `ERROR:`、`SCRIPT ERROR:`、未处理异常及结构化 Error/Critical 日志。
- 稳定性与打包开始前必须先完成 Art 素材审计和 Godot 导入，避免缺失 `.ctex` 缓存产生假通过。
- Debug/Release 门禁重新生成十万次经济审计与六构筑战斗审计，并与仓库封版报告逐字比较。
- CI 使用与本地相同的 Release 门禁，避免两套判断规则漂移。

## 发布顺序

1. `git lfs pull` 与 `git lfs fsck`。
2. Debug、Release 完整 `verify.ps1`。
3. Visible/Tray 长稳与 Offline48h 等价测试。
4. Windows Release 导出并生成 `GameForWork-v0.2.0-win-x64.zip`。
5. 从独立解压目录执行候选包人工验收。
6. 用户确认后创建 `v0.2.0` 标签与 GitHub Release。

正式标签仍不由 Release.1 自动创建。
