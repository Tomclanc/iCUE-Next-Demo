# Widget dependencies

CORSAIR/iCUE names and icons belong to their respective owners. This is an unofficial personal project; no ownership or new rights over those assets are claimed. No official CORSAIR executable or DLL is included.

The binary release contains unmodified dynamically linked dependencies. License texts are included in `licenses/` in the download and `ThirdPartyLicenses/` inside each package.

| Component | License / corresponding upstream source |
|---|---|
| PySide6 / Shiboken 6.8.3 | LGPL-3.0 option; https://code.qt.io/cgit/pyside/pyside-setup.git/tree/?h=v6.8.3 |
| Qt Core, Network, RemoteObjects 6.8.3 | LGPL-3.0 option; https://code.qt.io/cgit/qt/qtbase.git/tree/?h=v6.8.3 and https://code.qt.io/cgit/qt/qtremoteobjects.git/tree/?h=v6.8.3 |
| Python 3.12 runtime | PSF license and included third-party notices; https://github.com/python/cpython/tree/3.12 |
| PyInstaller 6.22.3 bootloader | GPL with distribution exception, see PyInstaller.txt; https://github.com/pyinstaller/pyinstaller/tree/v6.22.3 |
| .NET 8 runtime / Windows Desktop | MIT and bundled third-party notices; https://github.com/dotnet/runtime and https://github.com/dotnet/winforms |
| Windows App SDK 2.3.1 and WebView2 / Windows SDK support libraries | Microsoft package licenses, included notices; https://github.com/microsoft/WindowsAppSDK |
| OpenSSL 3 / libffi | Apache-2.0 / permissive license, included texts |
| Microsoft Visual C++ runtime / Universal CRT | Microsoft redistributable runtime terms |

The bridge is built with the separately replaceable Qt/PySide DLLs, not statically linked Qt. No restriction is imposed on modification of those libraries or reverse engineering to debug such modifications. The bridge source and build instructions are published alongside the package. Qt/PySide sources and build instructions are available from the versioned upstream trees above; LGPL-3.0 and GPL-3.0 texts accompany the package.
