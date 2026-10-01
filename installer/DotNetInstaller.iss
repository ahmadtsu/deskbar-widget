[Code]
procedure AddDotNet90DesktopDependency;
begin
  // Check if .NET Desktop Runtime 9.0.x is installed
  if not Dependency_IsNetCoreInstalled('Microsoft.WindowsDesktop.App', 9, 0, 0) then begin
    Dependency_Add('dotnet90desktop' + Dependency_ArchSuffix + '.exe',
      '/lcid ' + IntToStr(GetUILanguage) + ' /passive /norestart',
      '.NET Desktop Runtime 9.0' + Dependency_ArchTitle,
      Dependency_String('https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/9.0.6/windowsdesktop-runtime-9.0.6-win-x86.exe',
                        'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/9.0.6/windowsdesktop-runtime-9.0.6-win-x64.exe'),
      '', False, False);
  end;
end;
