# Builds libgit2 for Android with the NDK, for each ABI the proof ships, into
# %LOCALAPPDATA%\Memoria-ToDo-proofs\libgit2-android\<abi>\libgit2-<short sha>.so, the name LibGit2Sharp loads.
# Needs the NDK and the SDK's CMake package (it includes ninja) under %LOCALAPPDATA%\Memoria-ToDo-proofs\android-sdk.
$ErrorActionPreference = 'Stop'

# The libgit2 commit LibGit2Sharp 0.32.0 binds to: libgit2\libgit2_hash.txt in LibGit2Sharp.NativeBinaries 2.0.324.
$sha = '5853918c4c6a7b12f8becf4bd11ff4362ebb9020'
$name = "git2-$($sha.Substring(0, 7))"
$abis = 'arm64-v8a', 'x86_64'

$home2 = Join-Path $env:LOCALAPPDATA 'Memoria-ToDo-proofs'
$ndk = Join-Path $home2 'android-sdk\ndk\30.0.16248370'
$cmakeBin = Join-Path $home2 'android-sdk\cmake\3.31.6\bin'
$srcRoot = Join-Path $home2 'libgit2-src'
$src = Join-Path $srcRoot "libgit2-$sha"
$out = Join-Path $home2 'libgit2-android'

if (-not (Test-Path $src)) {
    New-Item -ItemType Directory -Force $srcRoot | Out-Null
    $archive = Join-Path $srcRoot "libgit2-$sha.tar.gz"
    curl.exe -sSL --fail -o $archive "https://github.com/libgit2/libgit2/archive/$sha.tar.gz"
    if ($LASTEXITCODE -ne 0) { throw 'Downloading libgit2 failed.' }
    tar -xzf $archive -C $srcRoot
    if ($LASTEXITCODE -ne 0) { throw 'Extracting libgit2 failed.' }
}

foreach ($abi in $abis) {
    $build = Join-Path $home2 "libgit2-build\$abi"
    $started = [Diagnostics.Stopwatch]::StartNew()
    # libgit2 compiles as strict C90, and NDK r30's kernel headers use `static inline`, which C90 lacks.
    & "$cmakeBin\cmake.exe" -S $src -B $build -G Ninja --fresh `
        "-DCMAKE_MAKE_PROGRAM=$cmakeBin\ninja.exe" `
        "-DCMAKE_TOOLCHAIN_FILE=$ndk\build\cmake\android.toolchain.cmake" `
        "-DANDROID_ABI=$abi" -DANDROID_PLATFORM=android-26 `
        '-DCMAKE_C_FLAGS=-Dinline=__inline__' `
        -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=ON "-DLIBGIT2_FILENAME=$name" `
        -DBUILD_TESTS=OFF -DBUILD_CLI=OFF -DBUILD_EXAMPLES=OFF `
        -DUSE_SSH=OFF -DUSE_HTTPS=OFF -DUSE_NTLMCLIENT=OFF -DUSE_GSSAPI=OFF `
        -DUSE_BUNDLED_ZLIB=ON -DREGEX_BACKEND=builtin | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Configuring libgit2 for $abi failed." }
    & "$cmakeBin\cmake.exe" --build $build --parallel
    if ($LASTEXITCODE -ne 0) { throw "Building libgit2 for $abi failed." }

    $built = Get-ChildItem $build -Recurse -Filter "lib$name.so" | Select-Object -First 1
    if (-not $built) { throw "The build for $abi made no lib$name.so." }
    $target = Join-Path $out $abi
    New-Item -ItemType Directory -Force $target | Out-Null
    # The NDK compiles with debug info; the APK ships the library without it.
    $shipped = Join-Path $target "lib$name.so"
    & "$ndk\toolchains\llvm\prebuilt\windows-x86_64\bin\llvm-strip.exe" --strip-unneeded -o $shipped $built.FullName
    if ($LASTEXITCODE -ne 0) { throw "Stripping libgit2 for $abi failed." }
    "{0}: lib{1}.so, {2:N0} KB stripped, built in {3:N0} s" -f $abi, $name, ((Get-Item $shipped).Length / 1KB), $started.Elapsed.TotalSeconds
}
