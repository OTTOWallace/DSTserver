[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=DSTserver 安装完成：桌面与开始菜单已创建快捷方式。首次使用请点击「安装 / 更新饥荒服务器」下载服务器本体（约 4.3 GB）。
TargetName=DSTServer_Setup.exe
FriendlyName=DSTserver - Don't Starve Together Server Launcher
AppLaunched=cmd /c postinstall.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=cmd /c postinstall.cmd
UserQuietInstCmd=cmd /c postinstall.cmd
SourceFiles=SourceFiles
[Strings]
FILE0="bootstrap.ps1"
FILE1="DSTserver.deps.json"
FILE2="DSTserver.dll"
FILE3="DSTserver.exe"
FILE4="DSTserver.pdb"
FILE5="DSTserver.runtimeconfig.json"
FILE6="LICENSE"
FILE7="postinstall.cmd"
FILE8="README.md"
FILE9="settings.json"
[SourceFiles]
SourceFiles0=C:\VSPJ\installer\payload\
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
%FILE3%=
%FILE4%=
%FILE5%=
%FILE6%=
%FILE7%=
%FILE8%=
%FILE9%=