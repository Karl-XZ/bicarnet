# bicarnet activation codes

New Windows and Android clients require one one-time activation code before the first connection. After activation, a local device token is saved and the same device will not ask again. Older clients do not call this endpoint and are not affected.

Generate and package activation codes:

```powershell
.\scripts\common\New-ActivationCodes.ps1 -Count 20 -Force
.\scripts\common\New-ActivationCodes.ps1 -Count 3 -Role admin -Append
.\scripts\common\Build-WindowsClient.ps1
.\scripts\common\Build-WindowsPureClient.ps1
.\scripts\common\Build-WindowsAdmin.ps1
.\scripts\common\Build-Android.ps1
```

Files:

- Plain one-time codes: `runtime/activation/activation-codes-plain.txt`
- Server hash store: `runtime/activation/activation-codes.json`
- Packaged server hash store: `dist/windows/activation-codes.json`
- Admin client exe: `dist/windows-admin/bicarnet-admin.exe`

The public VPN tunnel still uses UDP `51820`. First-time public activation also needs the server status API reachable on TCP `8787` at `/activate`; LAN activation can use the same TCP `8787` endpoint over WiFi.

Admin clients use admin-only activation codes. After joining the VPN, the admin UI can read `/admin/devices`, block devices through `/admin/block`, and restore them through `/admin/unblock`. Admin requests require the locally stored admin activation token and do not work with normal client activation codes.
