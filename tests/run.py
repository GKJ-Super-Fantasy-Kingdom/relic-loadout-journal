"""Check the local Thunderstore payload in embedded game Mono, using a copied save."""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
from tempfile import TemporaryDirectory
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
MOD = "RelicLoadoutJournal"


def worker(game, core, exe, dll, save):
    managed = game / "Super Fantasy Kingdom_Data/Managed"
    runtime = game / "MonoBleedingEdge"
    paths = os.pathsep.join(map(str, (managed, core, exe.parent)))
    os.environ["MONO_PATH"] = paths
    with os.add_dll_directory(str(runtime / "EmbedRuntime")):
        mono = ctypes.CDLL(str(runtime / "EmbedRuntime/mono-2.0-bdwgc.dll"))

    def api(name, result, *types):
        fn = getattr(mono, name)
        fn.restype, fn.argtypes = result, types
        return fn

    ptr, string, num = ctypes.c_void_p, ctypes.c_char_p, ctypes.c_int
    api("mono_set_dirs", None, string, string)(str(managed).encode(), str(runtime / "etc").encode())
    api("mono_set_assemblies_path", None, string)(paths.encode())
    domain = api("mono_jit_init_version", ptr, string, string)(b"JournalChecks", b"v4.0.30319")
    register = api("mono_add_internal_call", None, string, ptr)
    new_string = api("mono_string_new", ptr, ptr, string)
    get_class = api("mono_object_get_class", ptr, ptr)
    parent = api("mono_class_get_parent", ptr, ptr)
    field = api("mono_class_get_field_from_name", ptr, ptr, string)
    field_value = api("mono_field_get_value_object", ptr, ptr, ptr, ptr)

    # Supply only the native engine services needed by the completion hook.
    # Test relics mirror SpawnRelic's assignment of the prefab name to the held object.
    @ctypes.CFUNCTYPE(ptr, ptr)
    def object_name(obj):
        return field_value(domain, field(parent(get_class(obj)), b"identifier"), obj)

    @ctypes.CFUNCTYPE(ptr)
    def persistent_path():
        return new_string(domain, str(exe.parent).encode())

    @ctypes.CFUNCTYPE(num, ptr, num)
    def preference(key, default):
        return default

    register(b"UnityEngine.Object::GetName", object_name)
    register(b"UnityEngine.Application::get_persistentDataPath", persistent_path)
    register(b"UnityEngine.PlayerPrefs::GetInt(string,int)", preference)
    # Harmony compiles the title-scene methods while binding patches. Rendering them
    # would make this a player test, so fail if any scene-only service is invoked.
    def unexpected(*args):
        print("Unexpected title scene call in isolated check", file=sys.stderr, flush=True)
        os._exit(2)

    scene_only = {
        b"UnityEngine.GameObject::SetActive": ctypes.CFUNCTYPE(None, ptr, num)(unexpected),
        b"UnityEngine.GameObject::get_activeSelf": ctypes.CFUNCTYPE(num, ptr)(unexpected),
        b"UnityEngine.Application::get_version": ctypes.CFUNCTYPE(ptr)(unexpected),
    }
    for name, callback in scene_only.items():
        register(name, callback)
    argv = [str(arg).encode() for arg in (exe, dll, save, exe.parent)]
    assembly = api("mono_domain_assembly_open", ptr, ptr, string)(domain, argv[0])
    if not assembly:
        raise RuntimeError("Could not open the isolated check executable.")
    return api("mono_jit_exec", num, ptr, ptr, num, ctypes.POINTER(string))(
        domain, assembly, len(argv), (string * len(argv))(*argv))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--save", type=Path, required=True, help="Copy of a profile's *_game.data")
    parser.add_argument("--game", type=Path, default=Path(r"D:\SteamLibrary\steamapps\common\Super Fantasy Kingdom"))
    parser.add_argument("--profile", type=Path, default=Path(os.environ["APPDATA"]) /
                        "Thunderstore Mod Manager/DataFolder/SuperFantasyKingdom/profiles/Relic Loadout Journal Dev")
    args = parser.parse_args()
    game = args.game.resolve()
    core = args.profile.resolve() / "BepInEx/core"
    managed = game / "Super Fantasy Kingdom_Data/Managed"
    save = args.save.resolve()
    before = hashlib.sha256(save.read_bytes()).digest()
    version = json.loads((ROOT / "thunderstore/manifest.json").read_text())["version_number"]
    with TemporaryDirectory() as temp:
        exe = Path(temp) / "RunChecks.exe"
        dll = exe.with_name(MOD + ".dll")
        with ZipFile(ROOT / "dist" / f"{MOD}-{version}.zip") as archive:
            expected = {"LICENSE", "CHANGELOG.md", "README.md", "manifest.json", "icon.png", f"plugins/{MOD}.dll"}
            assert set(archive.namelist()) == expected and len(archive.namelist()) == len(expected)
            assert archive.testzip() is None
            assert struct.unpack(">II", archive.read("icon.png")[16:24]) == (256, 256)
            payload = archive.read(f"plugins/{MOD}.dll")
        assert payload == (ROOT / f"bin/Release/netstandard2.1/{MOD}.dll").read_bytes()
        dll.write_bytes(payload)
        refs = [managed / name for name in ("Assembly-CSharp.dll", "UnityEngine.CoreModule.dll", "netstandard.dll",
                                            "Newtonsoft.Json.dll", "UnityEngine.dll", "UnityEngine.UI.dll", "UnityEngine.UIModule.dll")]
        refs.extend([core / "0Harmony.dll", core / "Mono.Cecil.dll"])
        compiler = Path(os.environ["WINDIR"]) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
        subprocess.run([str(compiler), "/nologo", f"/out:{exe}", *(f"/reference:{p}" for p in refs),
                        str(Path(__file__).with_name("RunChecks.cs"))], check=True)
        subprocess.run([sys.executable, str(Path(__file__).resolve()), "--worker",
                        str(game), str(core), str(exe), str(dll), str(save)], cwd=temp, check=True)
    assert before == hashlib.sha256(save.read_bytes()).digest()
    print("PASS package contents, 256 px icon, matching Release payload and unchanged copied save")


if __name__ == "__main__":
    if sys.argv[1:2] == ["--worker"]:
        sys.exit(worker(*map(Path, sys.argv[2:])))
    main()
