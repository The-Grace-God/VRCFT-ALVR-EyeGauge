# VRCFT-ALVR-EyeGauge

VRCFaceTracking module for ALVR with added support for EyeGauge

Adds eye dilation by seeing how bright your screen is

## Installation
1. Copy "ALVRModule.dll" to C:\Users\\{user}\AppData\Roaming\VRCFaceTracking\CustomLibs\
2. Download the zip from https://github.com/The-Grace-God/VRCFT-ALVR-EyeGauge/releases/tag/!Misc-EyeGauge
3. Unpack zip to C:\Users\\{user}\AppData\Roaming\VRCFaceTracking\CustomLibs\ 

## Build from source

### Prerequisites

* ALVR (nightly)
* VRCFaceTracking
* Visual Studio

### Build

1. Download this repository.
2. Build project `ALVRModule` in the Visual Studio solution. The module will be ready to use.

## Usage

1. Launch VRCFaceTracking. Uninstall all modules except `ALVRModule`.
2. Launch ALVR. Set `Eye and face tracking` to `VRCFaceTracking`. Then connect the client.
3. Launch VRChat and enable face tracking in the settings.

- CVR Note: Launch after the game has been booted otherwise it wont be detected and data wont be sent
- VRC Note: VRCFaceTracking and ALVR can be launched in any order. If you launch VRCFaceTracking after VRChat, you need to reload your avatar.
