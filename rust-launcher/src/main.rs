#![windows_subsystem = "windows"]

use std::collections::HashMap;
use std::env;
use std::fs;
use std::os::windows::process::CommandExt;
use std::path::{Path, PathBuf};
use std::process::Command;

static BUNDLE_TAR_XZ: &[u8] = include_bytes!("../../dist/bundle.tar.xz");
const BUNDLE_VERSION: &str = "1.0.4-adguard";
const MANIFEST_URL: &str =
    "https://raw.githubusercontent.com/abdifaizal/PlayBrwsr/main/updates/manifest.json";
const CREATE_NO_WINDOW: u32 = 0x08000000;

#[link(name = "user32")]
extern "system" {
    fn MessageBoxW(hwnd: *const u8, text: *const u16, caption: *const u16, utype: u32) -> i32;
}

#[link(name = "kernel32")]
extern "system" {
    fn SetFileAttributesW(lpFileName: *const u16, dwFileAttributes: u32) -> i32;
}

fn to_wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

fn show_msg(title: &str, msg: &str, flags: u32) {
    let text = to_wide(msg);
    let cap = to_wide(title);
    unsafe {
        MessageBoxW(std::ptr::null(), text.as_ptr(), cap.as_ptr(), flags);
    }
}

fn set_hidden(path: &Path) {
    if let Some(s) = path.to_str() {
        let wide = to_wide(s);
        unsafe {
            SetFileAttributesW(wide.as_ptr(), 0x02);
        }
    }
}

fn is_dir_writable(dir: &Path) -> bool {
    let probe = dir.join(".playbrwsr_write_test");
    if fs::write(&probe, b"ok").is_ok() {
        let _ = fs::remove_file(&probe);
        true
    } else {
        false
    }
}

fn resolve_work_dir() -> PathBuf {
    if let Ok(exe) = env::current_exe() {
        if let Some(exe_dir) = exe.parent() {
            if is_dir_writable(exe_dir) {
                let p = exe_dir.join(".playbrwsr_runtime");
                let _ = fs::create_dir_all(&p);
                set_hidden(&p);
                return p;
            }
        }
    }
    let fallback = env::temp_dir().join("PlayBrwsrRuntime");
    let _ = fs::create_dir_all(&fallback);
    fallback
}

fn ensure_extracted(work_dir: &Path) -> Result<(), String> {
    let marker = work_dir.join(".bundle_version");
    let ff_exe = work_dir.join("runtime").join("firefox").join("firefox.exe");
    if ff_exe.exists() {
        if let Ok(ver) = fs::read_to_string(&marker) {
            if ver.trim() == BUNDLE_VERSION {
                return Ok(());
            }
        }
    }

    let tar_path = work_dir.join("_bundle.tar.xz");
    fs::write(&tar_path, BUNDLE_TAR_XZ)
        .map_err(|e| format!("Gagal menulis arsip sementara: {}", e))?;

    let status = Command::new("tar.exe")
        .arg("-xJf")
        .arg(&tar_path)
        .arg("-C")
        .arg(work_dir)
        .creation_flags(CREATE_NO_WINDOW)
        .status();

    let _ = fs::remove_file(&tar_path);

    let exit = status.map_err(|e| format!("Gagal menjalankan tar.exe: {}", e))?;
    if !exit.success() {
        return Err(format!("Ekstraksi arsip gagal (kode: {:?})", exit.code()));
    }

    fs::write(&marker, BUNDLE_VERSION)
        .map_err(|e| format!("Gagal menulis marker versi: {}", e))?;
    Ok(())
}

struct ComponentSpec {
    name: String,
    version: String,
    url: String,
    target: String,
    sha256: String,
}

fn extract_json_str(block: &str, key: &str) -> Option<String> {
    let pat = format!("\"{}\"", key);
    let idx = block.find(&pat)?;
    let rest = &block[idx + pat.len()..];
    let colon = rest.find(':')?;
    let after_colon = rest[colon + 1..].trim_start();
    if !after_colon.starts_with('"') {
        return None;
    }
    let inner = &after_colon[1..];
    let end = inner.find('"')?;
    Some(inner[..end].to_string())
}

fn parse_manifest(json: &str) -> Vec<ComponentSpec> {
    let mut out = Vec::new();
    let Some(comp_pos) = json.find("\"components\"") else {
        return out;
    };
    let rest = &json[comp_pos..];
    let Some(open_brace) = rest.find('{') else {
        return out;
    };
    let mut cursor = &rest[open_brace + 1..];

    while let Some(q1) = cursor.find('"') {
        let after_q1 = &cursor[q1 + 1..];
        let Some(q2) = after_q1.find('"') else { break };
        let name = after_q1[..q2].to_string();
        let after_name = &after_q1[q2 + 1..];
        let Some(obj_start) = after_name.find('{') else { break };
        let obj_body = &after_name[obj_start + 1..];
        let Some(obj_end) = obj_body.find('}') else { break };
        let block = &obj_body[..obj_end];

        if let (Some(version), Some(url), Some(target), Some(sha256)) = (
            extract_json_str(block, "version"),
            extract_json_str(block, "url"),
            extract_json_str(block, "target"),
            extract_json_str(block, "sha256"),
        ) {
            out.push(ComponentSpec {
                name,
                version,
                url,
                target,
                sha256: sha256.to_lowercase(),
            });
        }

        cursor = &obj_body[obj_end + 1..];
    }
    out
}

fn is_safe_rel_path(rel: &str) -> bool {
    !rel.is_empty()
        && !rel.starts_with('/')
        && !rel.starts_with('\\')
        && !rel.contains(':')
        && !rel.split(['/', '\\']).any(|seg| seg == "..")
}

fn is_allowed_url(url: &str) -> bool {
    url.starts_with("https://raw.githubusercontent.com/abdifaizal/PlayBrwsr/")
        || url.starts_with("https://github.com/abdifaizal/PlayBrwsr/")
}

fn file_sha256(path: &Path) -> Option<String> {
    let out = Command::new("certutil.exe")
        .arg("-hashfile")
        .arg(path)
        .arg("SHA256")
        .creation_flags(CREATE_NO_WINDOW)
        .output()
        .ok()?;
    if !out.status.success() {
        return None;
    }
    let text = String::from_utf8_lossy(&out.stdout);
    for line in text.lines() {
        let clean: String = line.trim().chars().filter(|c| c.is_ascii_hexdigit()).collect();
        if clean.len() == 64 {
            return Some(clean.to_lowercase());
        }
    }
    None
}

fn fetch_url_to_file(url: &str, dest: &Path, max_secs: &str) -> bool {
    let status = Command::new("curl.exe")
        .arg("-fsSL")
        .arg("--connect-timeout")
        .arg("3")
        .arg("--max-time")
        .arg(max_secs)
        .arg("-o")
        .arg(dest)
        .arg(url)
        .creation_flags(CREATE_NO_WINDOW)
        .status();
    matches!(status, Ok(s) if s.success())
}

fn load_state(state_path: &Path) -> HashMap<String, String> {
    let mut map = HashMap::new();
    if let Ok(txt) = fs::read_to_string(state_path) {
        for line in txt.lines() {
            if let Some((k, v)) = line.split_once('=') {
                map.insert(k.trim().to_string(), v.trim().to_string());
            }
        }
    }
    map
}

fn save_state(state_path: &Path, map: &HashMap<String, String>) {
    let mut lines: Vec<String> = map.iter().map(|(k, v)| format!("{}={}", k, v)).collect();
    lines.sort();
    let _ = fs::write(state_path, lines.join("\n"));
}

fn check_and_update_components(work_dir: &Path) -> Result<Vec<String>, String> {
    let manifest_tmp = work_dir.join("_manifest.json");
    if !fetch_url_to_file(MANIFEST_URL, &manifest_tmp, "5") {
        let _ = fs::remove_file(&manifest_tmp);
        return Err("Tidak dapat terhubung ke server update GitHub.".to_string());
    }
    let manifest_txt = fs::read_to_string(&manifest_tmp)
        .map_err(|e| format!("Gagal membaca manifest: {}", e))?;
    let _ = fs::remove_file(&manifest_tmp);

    let specs = parse_manifest(&manifest_txt);
    if specs.is_empty() {
        return Err("Manifest update kosong atau tidak valid.".to_string());
    }

    let state_path = work_dir.join(".component_state");
    let mut state = load_state(&state_path);
    let mut updated = Vec::new();

    for comp in specs {
        if !is_safe_rel_path(&comp.target) || !is_allowed_url(&comp.url) {
            continue;
        }
        let target_path = work_dir.join(comp.target.replace('/', "\\"));
        let current_hash = if target_path.exists() {
            file_sha256(&target_path).unwrap_or_default()
        } else {
            String::new()
        };

        let expected_tag = format!("{}:{}", comp.version, comp.sha256);
        let already_matches = state.get(&comp.name).map(|s| s == &expected_tag).unwrap_or(false)
            || (!comp.sha256.is_empty() && current_hash == comp.sha256);

        if already_matches {
            state.insert(comp.name.clone(), expected_tag);
            continue;
        }

        if let Some(parent) = target_path.parent() {
            let _ = fs::create_dir_all(parent);
        }
        let tmp_dl = work_dir.join(format!("_dl_{}.tmp", comp.name));
        if !fetch_url_to_file(&comp.url, &tmp_dl, "25") {
            let _ = fs::remove_file(&tmp_dl);
            continue;
        }

        if !comp.sha256.is_empty() {
            let dl_hash = file_sha256(&tmp_dl).unwrap_or_default();
            if dl_hash != comp.sha256 {
                let _ = fs::remove_file(&tmp_dl);
                continue;
            }
        }

        if fs::rename(&tmp_dl, &target_path).is_err() {
            let _ = fs::copy(&tmp_dl, &target_path);
            let _ = fs::remove_file(&tmp_dl);
        }

        if comp.target == "extensions/browsec@browsec.com.xpi"
            || comp.target == "extensions/playbrwsr-adguard@local.xpi"
        {
            let file_name = Path::new(&comp.target)
                .file_name()
                .unwrap_or_default();
            let profile_ext = work_dir
                .join("data")
                .join("profile")
                .join("extensions")
                .join(file_name);
            if let Some(p) = profile_ext.parent() {
                let _ = fs::create_dir_all(p);
            }
            let _ = fs::copy(&target_path, &profile_ext);
        }

        state.insert(comp.name.clone(), expected_tag);
        updated.push(format!("{} (v{})", comp.name, comp.version));
    }

    save_state(&state_path, &state);
    Ok(updated)
}

fn path_to_file_uri(path: &Path) -> String {
    let abs = fs::canonicalize(path).unwrap_or_else(|_| path.to_path_buf());
    let s = abs.to_string_lossy().replace('\\', "/");
    let trimmed = s.strip_prefix("//?/").unwrap_or(&s);
    format!("file:///{}", trimmed)
}

fn sync_policies(work_dir: &Path) -> Result<(), String> {
    let ext_path = work_dir.join("extensions").join("browsec@browsec.com.xpi");
    let dist_dir = work_dir.join("runtime").join("firefox").join("distribution");
    fs::create_dir_all(&dist_dir).map_err(|e| e.to_string())?;

    let ext_uri = path_to_file_uri(&ext_path);
    let policies = format!(
        r#"{{
  "policies": {{
    "DisableAppUpdate": true,
    "DontCheckDefaultBrowser": true,
    "DisableTelemetry": true,
    "DisablePocket": true,
    "DisableFirefoxAccounts": true,
    "OverrideFirstRunPage": "",
    "OverridePostUpdatePage": "",
    "ExtensionSettings": {{
      "browsec@browsec.com": {{
        "installation_mode": "force_installed",
        "install_url": "{}",
        "default_area": "navbar"
      }}
    }}
  }}
}}
"#,
        ext_uri.replace('"', "\\\"")
    );

    fs::write(dist_dir.join("policies.json"), policies).map_err(|e| e.to_string())
}

fn run() -> Result<(), String> {
    let work_dir = resolve_work_dir();
    ensure_extracted(&work_dir)?;

    let args: Vec<String> = env::args().skip(1).collect();
    let mode = args.first().map(|s| s.to_lowercase()).unwrap_or_default();

    if mode == "--update" {
        match check_and_update_components(&work_dir) {
            Ok(updated) if updated.is_empty() => {
                show_msg(
                    "PlayBrwsr Updater",
                    "Semua komponen sudah menggunakan versi terbaru.",
                    0x40,
                );
            }
            Ok(updated) => {
                show_msg(
                    "PlayBrwsr Updater",
                    &format!("Komponen berhasil diperbarui:\n• {}", updated.join("\n• ")),
                    0x40,
                );
            }
            Err(e) => {
                show_msg("PlayBrwsr Updater", &e, 0x30);
            }
        }
        return Ok(());
    }

    if mode != "--no-update" {
        let _ = check_and_update_components(&work_dir);
    }

    sync_policies(&work_dir)?;

    let ff_exe = work_dir.join("runtime").join("firefox").join("firefox.exe");
    let ff_dir = work_dir.join("runtime").join("firefox");
    let profile_dir = work_dir.join("data").join("profile");

    let sys_roaming = work_dir.join("data").join("sys").join("roaming");
    let sys_local = work_dir.join("data").join("sys").join("local");
    let sys_temp = work_dir.join("data").join("sys").join("temp");
    let _ = fs::create_dir_all(&sys_roaming);
    let _ = fs::create_dir_all(&sys_local);
    let _ = fs::create_dir_all(&sys_temp);
    let _ = fs::create_dir_all(&profile_dir);

    for ext_name in ["browsec@browsec.com.xpi", "playbrwsr-adguard@local.xpi"] {
        let ext_src = work_dir.join("extensions").join(ext_name);
        let profile_ext_dir = profile_dir.join("extensions");
        let ext_dst = profile_ext_dir.join(ext_name);
        if ext_src.exists() && !ext_dst.exists() {
            let _ = fs::create_dir_all(&profile_ext_dir);
            let _ = fs::copy(&ext_src, &ext_dst);
        }
    }

    let mut urls: Vec<String> = Vec::new();
    match mode.as_str() {
        "--video" => urls.push("https://www.youtube.com".to_string()),
        "--music" => urls.push("https://music.youtube.com".to_string()),
        "--hub" => {
            let hub = work_dir.join("app").join("hub.html");
            urls.push(path_to_file_uri(&hub));
        }
        _ => {
            urls.push("https://www.youtube.com".to_string());
            urls.push("https://music.youtube.com".to_string());
        }
    }

    let mut cmd = Command::new(&ff_exe);
    cmd.current_dir(&ff_dir)
        .arg("-no-remote")
        .arg("-profile")
        .arg(&profile_dir);
    for u in urls {
        cmd.arg(u);
    }

    cmd.env("APPDATA", &sys_roaming)
        .env("LOCALAPPDATA", &sys_local)
        .env("TEMP", &sys_temp)
        .env("TMP", &sys_temp);

    cmd.spawn()
        .map_err(|e| format!("Gagal menjalankan PlayBrwsr: {}", e))?;

    Ok(())
}

fn main() {
    if let Err(e) = run() {
        show_msg("PlayBrwsr Error", &e, 0x10);
    }
}
