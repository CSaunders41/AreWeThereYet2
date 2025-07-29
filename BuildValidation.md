# AreWeThereYet2 Build Validation Guide

## Build Requirements

### Expected Directory Structure
Your ExileApi installation should look like this:
```
C:\Users\Admin\Documents\POE1\ExileApi-Compiled-3.26.0.0.4\
├── ExileCore.dll                    ← Main ExileCore library
├── GameOffsets.dll                  ← Game memory offsets
├── Coroutine.dll                    ← Coroutine support
├── ImGui.NET.dll                    ← ImGui .NET bindings
├── SharpDX.dll                      ← DirectX wrapper
├── SharpDX.Mathematics.dll          ← DirectX math library
└── Plugins\
    └── Source\
        └── AreWeThereYet2\          ← Our plugin source
            ├── AreWeThereYet2.cs
            ├── AreWeThereYet2.csproj
            ├── Core\
            ├── Party\
            └── Settings\
```

## Build Troubleshooting

### 1. DLL Reference Issues
If you get "ExileCore could not be found" errors:

**Check DLL Locations:**
- Verify `ExileCore.dll` exists in: `C:\Users\Admin\Documents\POE1\ExileApi-Compiled-3.26.0.0.4\ExileCore.dll`
- Verify other DLLs are in the same directory

**If DLLs are in different locations, update .csproj:**
```xml
<Reference Include="ExileCore">
  <HintPath>PATH_TO_YOUR_EXILECORE_DLL</HintPath>
  <Private>false</Private>
</Reference>
```

### 2. Build Command
From the plugin directory, run:
```bash
dotnet build AreWeThereYet2.csproj
```

### 3. Manual Compilation
If dotnet build fails, try Visual Studio:
1. Open `AreWeThereYet2.csproj` in Visual Studio
2. Right-click project → "Build"
3. Check Output window for detailed errors

## Common Solutions

### Missing DLL References
1. **Right-click References** in Visual Studio
2. **Add Reference** → **Browse**
3. **Navigate** to ExileApi root directory
4. **Select required DLLs**:
   - ExileCore.dll
   - GameOffsets.dll
   - Coroutine.dll
   - ImGui.NET.dll
   - SharpDX.dll
   - SharpDX.Mathematics.dll

### Alternative .csproj Configuration
If the current paths don't work, try absolute paths:
```xml
<Reference Include="ExileCore">
  <HintPath>C:\Users\Admin\Documents\POE1\ExileApi-Compiled-3.26.0.0.4\ExileCore.dll</HintPath>
  <Private>false</Private>
</Reference>
```

## Build Success Indicators
✅ **Successful build produces:**
- `bin\Debug\net8.0-windows\AreWeThereYet2.dll`  
- No compilation errors
- Plugin ready for ExileCore loading

## Next Steps After Successful Build
1. Copy `AreWeThereYet2.dll` to ExileCore `Plugins` folder (not Source)
2. Restart ExileCore/PoE
3. Check plugin appears in ExileCore plugin list
4. Enable plugin and test functionality

## Support Information
- **Plugin Version**: Phase 0 Development
- **Target Framework**: .NET 8.0
- **ExileCore Version**: 3.26.0.0.4
- **Repository**: https://github.com/CSaunders41/AreWeThereYet2 