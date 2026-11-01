# StellovisMC Microsoft 登录配置

- 公开的 Application (client) ID：`415dda5e-8275-43a0-935c-f0347c3eb2d6`。
- 支持个人 Microsoft 账户，使用 `https://login.microsoftonline.com/consumers`。
- 当前登录方式：设备代码；权限：`XboxLive.signin offline_access`。
- 应用注册中需要启用「允许公共客户端流」。本桌面流程不使用 Client Secret。

默认 ID 位于 `PCL.Core/App/Secrets.cs`，以免本地 Release 构建因没有官方 CI 变量而丢失登录配置。已有编译时 `MS_CLIENT_ID` 注入仍优先，其他密钥的处理没有改变。

2026-11-01 检查：应用已开启公共客户端流；微软设备代码端点接受该 ID 和权限。用户本人完成授权后，Microsoft OAuth、Xbox Live 和 XSTS 均返回成功，Minecraft Services 的 `authentication/login_with_xbox` 返回 403。应用尚未提交 Minecraft API 准入申请，完整登录及游戏所有权尚未验证。

新注册应用的 Minecraft API 访问还需要完成申请，入口：<https://aka.ms/mce-reviewappid>。取得设备代码不代表该申请已获批准。
