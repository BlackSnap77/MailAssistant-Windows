# MailAssistant Windows

Separate Windows application baseline, developed with C#, .NET 8 and WinUI 3.
The Apple MailAssistant repository is not part of this solution.

## Develop in Visual Studio 2022

1. Install/update Visual Studio 2022 to version 17.14 with the .NET desktop and
   WinUI / Windows App SDK C# development components and a Windows SDK.
2. Ensure a .NET 8.0.4xx SDK is installed (the verified SDK is 8.0.425).
3. Open `MailAssistant.Windows.sln`.
4. Select `Debug | x64` and `MailAssistant.Windows` as the startup project.
5. Build, then start the `MailAssistant.Windows` launch profile with F5.

This is an unpackaged, self-contained x64 application. No MSIX signing certificate
or package deployment is required for this baseline. Windows 11 is the intended
development and use environment; the API target is Windows 10 SDK 19041 or later.

## Command-line build

```powershell
dotnet build .\MailAssistant.Windows.sln -c Debug -p:Platform=x64
```

NuGet versions are pinned and `packages.lock.json` is committed. For a restore
that checks the committed dependency graph, add `-p:RestoreLockedMode=true`.

## Baseline scope

The app displays a native WinUI welcome window. Mail accounts, IMAP, storage,
credentials and AI integration are deliberately future implementation work.
No external mail service is contacted by the application.

## Initial verification on this machine

- Repository: `J:\MailAssistant-Windows`
- SDK: .NET 8.0.425
- Windows App SDK: 1.8.260804001
- Configuration: Debug / x64
- Result: successful build, zero warnings, zero errors.
- Visual Studio was not installed at verification time; the build used a local
  SDK at `C:\Users\User\Documents\Codex\2026-09-22\referenced-chatgpt-conversation-this-is-an-5\work\dotnet\dotnet.exe`.
- The Visual Studio debugger and interactive application launch have not been
  verified. Install the development environment above for normal IDE work.

Build artifacts remain under the project's ignored `bin` and `obj` directories.
