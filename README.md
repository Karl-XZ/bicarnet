# DualNet Tunnel

本目录是一套可复现的双向网络互联基础版实现，目标是：

- 当前 Windows 电脑作为外网 WireGuard 服务器节点。
- 国内 Linux 服务器、Windows 客户端、Android 客户端都通过加密隧道接入。
- 后续 AC 服务、agent service 只需要绑定到隧道网段或通过隧道路由访问。
- Windows 端提供基础 exe，一键连接/断开/查看状态。
- Windows exe 可一键作为服务端启动，也可作为连接端启动。
- 服务端模式内置只读状态接口，用于 Windows/Android 显示当前连接设备。
- Android 端提供基础 App，使用 WireGuard 官方 tunnel library 调用 Android VPN 权限后一键连接。

## 架构

```text
Windows 外网服务器
  UDP 51820 / WireGuard
  10.77.0.1/24
        |
        | encrypted tunnel
        |
+-------+-------------------------------+
|                                       |
国内 Linux 服务器                      多终端客户端
10.77.0.2                              Windows: 10.77.0.10
AC service / agent service             Android: 10.77.0.11
```

## 前置条件

1. 外网服务器需要可被访问的公网 IPv4/IPv6 或路由器 UDP 端口映射。
2. Windows 外网服务器需要安装 WireGuard for Windows。
3. 外网服务器防火墙放行 UDP 51820。
4. Android APK 构建需要 Android SDK、JDK 17+、Gradle；本项目脚本会下载固定 Gradle 版本。

## 快速复现

在 PowerShell 中进入本目录：

```powershell
cd "C:\Users\Administrator\Documents\New project\dualnet-tunnel"
```

复制配置模板：

```powershell
Copy-Item .\config\site.example.json .\config\site.local.json
notepad .\config\site.local.json
```

至少修改：

- `server.publicEndpoint`: 外网服务器公网 IP 或域名。
- `server.listenPort`: 默认 51820。
- `clients`: 需要生成的客户端列表。

生成 WireGuard 配置：

```powershell
.\scripts\windows-server\01-generate-wireguard-config.ps1 -SiteConfig .\config\site.local.json
```

把生成的客户端配置写入 Windows/Android 客户端工程与分发目录：

```powershell
.\scripts\common\Package-GeneratedClients.ps1
```

安装并启动当前 Windows 电脑上的 WireGuard 服务端隧道：

```powershell
Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PWD\scripts\windows-server\02-install-wireguard-server.ps1`" -ServerConfig `"$PWD\runtime\server\dualnet-server.conf`""
```

构建 Windows 客户端 exe：

```powershell
.\scripts\common\Build-WindowsClient.ps1
.\scripts\common\Package-GeneratedClients.ps1
```

构建 Android APK：

```powershell
.\scripts\common\Package-GeneratedClients.ps1
.\scripts\common\Build-Android.ps1
```

## 产物位置

- 服务端 WireGuard 配置：`runtime/server/dualnet-server.conf`
- 客户端 WireGuard 配置：`runtime/clients/*.conf`
- 客户端 manifest：`runtime/clients/*.json`
- Windows exe：`dist/windows/DualNetClient.exe`
- Android APK：`dist/android/dualnet-client-debug.apk`
- 服务端状态接口：`http://10.77.0.1:8787/status`

## 地址策略

- `127.0.0.1:8787/status` 只用于服务端本机自检。
- `10.77.0.1:8787/status` 是 VPN 内部服务端地址，推荐固定，不依赖手机或电脑的物理 IP。
- `server.publicEndpoint` 和 `server.lanEndpoint` 是 WireGuard 握手入口，公网 IP、DDNS 或局域网 IP 可以变化。
- 如果物理地址变化，运行 `scripts/common/Update-Endpoints.ps1` 后重新生成配置和客户端。

## 安全说明

- 认证基于 WireGuard peer 公私钥和 preshared key，不使用明文密码作为 VPN 凭据。
- `runtime` 目录包含私钥，默认加入 `.gitignore`，不要上传到仓库或聊天工具。
- Windows 客户端需要管理员权限，因为安装/删除 WireGuard tunnel service 需要系统权限。
- Android 客户端第一次连接时会触发系统 VPN 授权弹窗，授权后可一键连接。

## 当前边界

- 本项目不绕过运营商、路由器或云厂商的端口限制；如果没有公网入口，需要另配中转 VPS 或内网穿透。
- 手机与 Windows 服务器处于同一 WiFi 时，优先使用局域网节点，例如 `192.168.0.104:51820`；外网/4G 才使用公网节点。
- Windows 服务端 NAT/转发脚本提供基础配置，复杂多网卡场景需要根据实际网卡名调整。
- Android 基础版把配置打包进 App assets；生产版建议增加后端登录、设备审批、配置下发和吊销。
