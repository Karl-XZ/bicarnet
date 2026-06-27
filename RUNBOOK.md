# DualNet 当前部署记录

生成时间：2026-06-26

## 本机外网服务器状态

- WireGuard for Windows：已安装。
- 服务端 tunnel：`dualnet-server`
- 服务状态：`RUNNING`
- 监听端口：UDP `51820`
- 服务端隧道地址：`10.77.0.1/24`
- 探测到的公网出口：`134.102.141.73`
- Windows 防火墙：已放行 `DualNet WireGuard UDP 51820`

验证命令：

```powershell
sc.exe query 'WireGuardTunnel$dualnet-server'
& 'C:\Program Files\WireGuard\wg.exe' show
Get-NetIPAddress -InterfaceAlias 'dualnet-server'
```

## 已生成客户端

- 国内服务器配置：`runtime/clients/domestic-linux.conf`
- Windows 客户端配置：`runtime/clients/dualnet-client-windows.conf`
- Android 客户端配置：`runtime/clients/dualnet-client-android.conf`
- Windows exe：`dist/windows/DualNetClient.exe`
- Android APK：`dist/android/dualnet-client-debug.apk`
- 服务端配置副本：`dist/windows/server/dualnet-server.conf`

## 使用方式

Windows：

1. 确保目标 Windows 客户端安装 WireGuard for Windows。
2. 复制 `dist/windows` 整个目录到目标机器。
3. 关闭所有旧 DualNet 窗口，以管理员身份重新运行新版 `DualNetClient.exe`。
4. 在“首页”选择角色：
   - 作为连接端时点击“一键连接”。
   - 作为服务端时点击“一键启动服务端”，会安装 WireGuard 服务端隧道并启动设备状态接口。
5. 在“设备”页点击“刷新设备”查看当前在线设备。
6. 普通用户不需要看日志；需要排错时再打开“高级”页查看诊断详情。

如果点击“一键启动服务端”没有明显反应：

- 确认运行的是 `dist/windows/DualNetClient.exe` 的新版，不是旧窗口。
- 新版点击后会立刻显示“正在启动”，成功后会弹出“服务端已就绪”。
- 如果仍失败，打开“高级”页查看诊断详情，或运行：

```powershell
sc.exe query 'WireGuardTunnel$dualnet-server'
& 'C:\Program Files\WireGuard\wg.exe' show dualnet-server
```

设备列表：

- Windows exe 可点击“从本机服务端读取”或“从状态接口读取”。
- Android App 可点击“刷新设备”，访问 `http://10.77.0.1:8787/status`。
- 状态接口需要 Windows 新版 exe 点击“一键启动服务端”或“启动状态接口”后才会开启。
- 在线判定：WireGuard 最近握手在 180 秒内。
- `127.0.0.1:8787/status` 只适合 Windows 服务端本机自检，手机和其他电脑不能用这个地址。
- `10.77.0.1:8787/status` 是 WireGuard 服务端隧道地址，推荐固定。只要服务端隧道地址仍是 `10.77.0.1/24`，不同手机、电脑、国内服务器都可以通过它看设备列表。
- 如果要改服务端隧道网段，例如改成 `10.88.0.1/24`，需要重跑配置生成，客户端里的状态接口会随之变成 `http://10.88.0.1:8787/status`。

Android：

1. 卸载手机上的旧 DualNet，安装新版 `dist/android/dualnet-client-debug.apk`。
2. 如果手机和这台 Windows 服务器在同一 WiFi，先点“局域网节点”，节点应显示 `192.168.0.104:51820`。
3. 如果手机在外网/4G，点“公网节点”，节点应显示 `134.102.141.73:51820`，同时路由器必须把 UDP `51820` 转发到 Windows 服务器。
4. 首次点击“连接”时允许系统 VPN 授权。
5. 首页会用大状态卡提示是否连接成功；如果显示“VPN 已启动，但服务端不可达”，优先检查节点选择、端口映射和防火墙。

国内 Linux 服务器：

```bash
sudo apt-get update
sudo apt-get install -y wireguard
sudo install -m 600 domestic-linux.conf /etc/wireguard/dualnet.conf
sudo systemctl enable --now wg-quick@dualnet
ip addr show dualnet
```

## 仍需人工确认

- 如果本机位于路由器 NAT 后面，需要在路由器把 UDP `51820` 转发到本机局域网地址。
- 如果公网 IP 会变化，建议把 `server.publicEndpoint` 改为动态 DNS 域名，然后重跑配置生成和客户端构建。
- 当前 Android APK 是 debug 签名，生产分发需要 release keystore 和签名流程。

## 地址变化处理

物理地址和隧道地址是两件事：

- 物理公网/LAN 地址用于 WireGuard 建立连接，可能变化，例如 `134.102.141.73` 或 `192.168.0.104`。
- 隧道服务端地址用于 VPN 内部访问，建议固定，例如 `10.77.0.1`。

公网或局域网地址变化后，更新配置：

```powershell
.\scripts\common\Update-Endpoints.ps1 -DetectPublicIp -DetectLanIp
.\scripts\windows-server\01-generate-wireguard-config.ps1 -SiteConfig .\config\site.local.json
.\scripts\common\Package-GeneratedClients.ps1
.\scripts\common\Build-Android.ps1
```

如果只改为域名：

```powershell
.\scripts\common\Update-Endpoints.ps1 -PublicEndpoint "your-ddns.example.com" -LanEndpoint "192.168.0.104"
```
