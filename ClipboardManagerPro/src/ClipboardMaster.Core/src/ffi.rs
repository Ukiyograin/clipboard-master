use std::ffi::{c_void, CStr, CString};
use std::os::raw::c_char;
use std::sync::Arc;
use parking_lot::RwLock;
use crate::{ClipboardCore, ClipboardItem, SearchQuery, AppSettings};

static mut CORE: Option<Arc<RwLock<Option<ClipboardCore>>>> = None;

#[no_mangle]
pub extern "C" fn clipboard_core_init() -> bool {
    unsafe {
        match ClipboardCore::new() {
            Ok(core) => {
                CORE = Some(Arc::new(RwLock::new(Some(core))));
                true
            }
            Err(e) => {
                log::error!("初始化失败: {}", e);
                false
            }
        }
    }
}

#[no_mangle]
pub extern "C" fn clipboard_core_start() -> bool {
    unsafe {
        if let Some(core_ref) = &CORE {
            let mut core_guard = core_ref.write();
            if let Some(core) = core_guard.as_mut() {
                match core.start() {
                    Ok(_) => true,
                    Err(e) => {
                        log::error!("启动失败: {}", e);
                        false
                    }
                }
            } else {
                false
            }
        } else {
            false
        }
    }
}

#[no_mangle]
pub extern "C" fn clipboard_core_stop() -> bool {
    unsafe {
        if let Some(core_ref) = &CORE {
            let core_guard = core_ref.read();
            if let Some(core) = core_guard.as_ref() {
                match core.stop() {
                    Ok(_) => true,
                    Err(e) => {
                        log::error!("停止失败: {}", e);
                        false
                    }
                }
            } else {
                false
            }
        } else {
            false
        }
    }
}

#[no_mangle]
pub extern "C" fn clipboard_core_get_settings() -> *mut c_char {
    unsafe {
        if let Some(core_ref) = &CORE {
            let core_guard = core_ref.read();
            if let Some(core) = core_guard.as_ref() {
                let settings = core.get_settings();
                match serde_json::to_string(&settings) {
                    Ok(json) => {
                        let c_string = CString::new(json).unwrap();
                        c_string.into_raw()
                    }
                    Err(e) => {
                        log::error!("序列化设置失败: {}", e);
                        std::ptr::null_mut()
                    }
                }
            } else {
                std::ptr::null_mut()
            }
        } else {
            std::ptr::null_mut()
        }
    }
}

#[no_mangle]
pub extern "C" fn clipboard_core_update_settings(settings_json: *const c_char) -> bool {
    unsafe {
        if settings_json.is_null() {
            return false;
        }
        
        if let Some(core_ref) = &CORE {
            let c_str = CStr::from_ptr(settings_json);
            let json_str = match c_str.to_str() {
                Ok(s) => s,
                Err(_) => return false,
            };
            
            let settings: AppSettings = match serde_json::from_str(json_str) {
                Ok(s) => s,
                Err(e) => {
                    log::error!("解析设置失败: {}", e);
                    return false;
                }
            };
            
            let core_guard = core_ref.read();
            if let Some(core) = core_guard.as_ref() {
                match core.update_settings(settings) {
                    Ok(_) => true,
                    Err(e) => {
                        log::error!("更新设置失败: {}", e);
                        false
                    }
                }
            } else {
                false
            }
        } else {
            false
        }
    }
}

#[no_mangle]
pub extern "C" fn clipboard_core_get_recent_items(limit: u32) -> *mut c_char {
    unsafe {
        if let Some(core_ref) = &CORE {
            let core_guard = core_ref.read();
            if let Some(core) = core_guard.as_ref() {
                match core.get_recent_items(limit) {
                    Ok(items) => {
                        match serde_json::to_string(&items) {
                            Ok(json) => {
                                let c_string = CString::new(json).unwrap();
                                c_string.into_raw()
                            }
                            Err(e) => {
                                log::error!("序列化项目失败: {}", e);
                                std::ptr::null_mut()
                            }
                        }
                    }
                    Err(e) => {
                        log::error!("获取项目失败: {}", e);
                        std::ptr::null_mut()
                    }
                }
            } else {
                std::ptr::null_mut()
            }
        } else {
            std::ptr::null_mut()
        }
    }
}

#[no_mangle]
pub extern "C" fn clipboard_core_free_string(ptr: *mut c_char) {
    unsafe {
        if !ptr.is_null() {
            let _ = CString::from_raw(ptr);
        }
    }
}