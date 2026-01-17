#![windows_subsystem = "windows"]

use std::collections::HashMap;
use std::path::{Path, PathBuf};
use std::sync::Arc;
use std::time::{Duration, SystemTime};
use parking_lot::RwLock;
use serde::{Deserialize, Serialize};
use chrono::{DateTime, Utc};
use uuid::Uuid;
use bytes::Bytes;
use crossbeam_channel::{Receiver, Sender};
use log::{info, warn, error};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum ClipboardContent {
    Text(String),
    Html(String),
    Image(ImageData),
    FileList(Vec<FileItem>),
    RichText(String),
    Custom(String, Vec<u8>),
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ImageData {
    pub data: Vec<u8>,
    pub width: u32,
    pub height: u32,
    pub format: ImageFormat,
    pub thumbnail: Vec<u8>,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub enum ImageFormat {
    Png,
    Jpeg,
    Bmp,
    Gif,
    Unknown,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FileItem {
    pub path: PathBuf,
    pub size: u64,
    pub modified: DateTime<Utc>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ClipboardItem {
    pub id: Uuid,
    pub content: ClipboardContent,
    pub timestamp: DateTime<Utc>,
    pub tags: Vec<String>,
    pub favorite: bool,
    pub pinned: bool,
    pub source_app: Option<String>,
    pub source_window: Option<String>,
    pub preview_text: String,
    pub preview_image: Option<Vec<u8>>,
    pub metadata: HashMap<String, String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SearchQuery {
    pub text: Option<String>,
    pub tags: Vec<String>,
    pub date_from: Option<DateTime<Utc>>,
    pub date_to: Option<DateTime<Utc>>,
    pub content_types: Vec<ContentType>,
    pub favorite_only: bool,
    pub pinned_only: bool,
    pub limit: Option<u32>,
    pub offset: Option<u32>,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub enum ContentType {
    Text,
    Image,
    File,
    Html,
    RichText,
    Custom,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AppSettings {
    pub max_items: u32,
    pub keep_days: u32,
    pub max_image_size_mb: u32,
    pub compress_images: bool,
    pub save_text: bool,
    pub save_images: bool,
    pub save_files: bool,
    pub save_html: bool,
    pub auto_cleanup: bool,
    pub startup_delay_ms: u32,
    pub database_path: String,
    pub cache_path: String,
    pub hotkeys: HotkeyConfig,
    pub ui: UiConfig,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct HotkeyConfig {
    pub show_window: String,
    pub pin_item: String,
    pub search: String,
    pub next_item: String,
    pub prev_item: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct UiConfig {
    pub theme: String,
    pub opacity: f32,
    pub animation_speed: u32,
    pub show_preview: bool,
    pub show_thumbnails: bool,
    pub thumbnail_size: u32,
    pub font_size: u32,
}

#[derive(Debug, Clone)]
pub enum ClipboardEvent {
    ItemAdded(ClipboardItem),
    ItemUpdated(ClipboardItem),
    ItemRemoved(Uuid),
    SettingsChanged(AppSettings),
    HotkeyPressed(String),
}

pub struct ClipboardCore {
    settings: Arc<RwLock<AppSettings>>,
    database: Arc<Database>,
    monitor: Option<ClipboardMonitor>,
    event_tx: Sender<ClipboardEvent>,
    event_rx: Receiver<ClipboardEvent>,
}

impl ClipboardCore {
    pub fn new() -> Result<Self, Box<dyn std::error::Error>> {
        let (event_tx, event_rx) = crossbeam_channel::unbounded();
        
        // 加载设置
        let settings = Self::load_settings()?;
        let settings = Arc::new(RwLock::new(settings));
        
        // 初始化数据库
        let database_path = settings.read().database_path.clone();
        let database = Database::new(&database_path)?;
        let database = Arc::new(database);
        
        Ok(Self {
            settings,
            database,
            monitor: None,
            event_tx,
            event_rx,
        })
    }
    
    pub fn start(&mut self) -> Result<(), Box<dyn std::error::Error>> {
        info!("Starting Clipboard Core...");
        
        // 启动监控器
        let monitor = ClipboardMonitor::new(
            self.settings.clone(),
            self.database.clone(),
            self.event_tx.clone(),
        )?;
        
        self.monitor = Some(monitor);
        self.monitor.as_ref().unwrap().start()?;
        
        info!("Clipboard Core started successfully");
        Ok(())
    }
    
    pub fn stop(&self) -> Result<(), Box<dyn std::error::Error>> {
        info!("Stopping Clipboard Core...");
        
        if let Some(monitor) = &self.monitor {
            monitor.stop()?;
        }
        
        // 执行清理
        self.cleanup_old_items()?;
        
        info!("Clipboard Core stopped");
        Ok(())
    }
    
    pub fn get_recent_items(&self, limit: u32) -> Result<Vec<ClipboardItem>, Box<dyn std::error::Error>> {
        self.database.get_recent_items(limit)
    }
    
    pub fn search_items(&self, query: SearchQuery) -> Result<Vec<ClipboardItem>, Box<dyn std::error::Error>> {
        self.database.search_items(query)
    }
    
    pub fn save_item(&self, item: ClipboardItem) -> Result<(), Box<dyn std::error::Error>> {
        self.database.save_item(item)
    }
    
    pub fn update_item(&self, item: ClipboardItem) -> Result<(), Box<dyn std::error::Error>> {
        self.database.update_item(item)
    }
    
    pub fn delete_item(&self, id: Uuid) -> Result<(), Box<dyn std::error::Error>> {
        self.database.delete_item(id)
    }
    
    pub fn get_item(&self, id: Uuid) -> Result<Option<ClipboardItem>, Box<dyn std::error::Error>> {
        self.database.get_item(id)
    }
    
    pub fn set_favorite(&self, id: Uuid, favorite: bool) -> Result<(), Box<dyn std::error::Error>> {
        self.database.set_favorite(id, favorite)
    }
    
    pub fn set_pinned(&self, id: Uuid, pinned: bool) -> Result<(), Box<dyn std::error::Error>> {
        self.database.set_pinned(id, pinned)
    }
    
    pub fn add_tags(&self, id: Uuid, tags: Vec<String>) -> Result<(), Box<dyn std::error::Error>> {
        self.database.add_tags(id, tags)
    }
    
    pub fn remove_tags(&self, id: Uuid, tags: Vec<String>) -> Result<(), Box<dyn std::error::Error>> {
        self.database.remove_tags(id, tags)
    }
    
    pub fn get_statistics(&self) -> Result<Statistics, Box<dyn std::error::Error>> {
        self.database.get_statistics()
    }
    
    pub fn cleanup_old_items(&self) -> Result<u32, Box<dyn std::error::Error>> {
        let settings = self.settings.read();
        let deleted = self.database.cleanup_old_items(settings.keep_days)?;
        
        info!("Cleaned up {} old items", deleted);
        Ok(deleted)
    }
    
    pub fn export_items(&self, path: &Path, format: ExportFormat) -> Result<(), Box<dyn std::error::Error>> {
        self.database.export_items(path, format)
    }
    
    pub fn import_items(&self, path: &Path) -> Result<u32, Box<dyn std::error::Error>> {
        self.database.import_items(path)
    }
    
    pub fn get_settings(&self) -> AppSettings {
        self.settings.read().clone()
    }
    
    pub fn update_settings(&self, settings: AppSettings) -> Result<(), Box<dyn std::error::Error>> {
        *self.settings.write() = settings.clone();
        Self::save_settings(&settings)?;
        
        // 发送设置变更事件
        self.event_tx.send(ClipboardEvent::SettingsChanged(settings))
            .map_err(|e| e.into())
    }
    
    pub fn receive_events(&self) -> &Receiver<ClipboardEvent> {
        &self.event_rx
    }
    
    fn load_settings() -> Result<AppSettings, Box<dyn std::error::Error>> {
        let config_dir = dirs::config_dir()
            .ok_or("无法获取配置目录")?
            .join("ClipboardMaster");
        
        std::fs::create_dir_all(&config_dir)?;
        
        let config_file = config_dir.join("config.json");
        
        if config_file.exists() {
            let content = std::fs::read_to_string(config_file)?;
            Ok(serde_json::from_str(&content)?)
        } else {
            let settings = Self::default_settings();
            let content = serde_json::to_string_pretty(&settings)?;
            std::fs::write(config_file, content)?;
            Ok(settings)
        }
    }
    
    fn save_settings(settings: &AppSettings) -> Result<(), Box<dyn std::error::Error>> {
        let config_dir = dirs::config_dir()
            .ok_or("无法获取配置目录")?
            .join("ClipboardMaster");
        
        std::fs::create_dir_all(&config_dir)?;
        
        let config_file = config_dir.join("config.json");
        let content = serde_json::to_string_pretty(settings)?;
        std::fs::write(config_file, content)?;
        
        Ok(())
    }
    
    fn default_settings() -> AppSettings {
        let config_dir = dirs::config_dir()
            .map(|p| p.join("ClipboardMaster").to_string_lossy().to_string())
            .unwrap_or_else(|| "C:\\ProgramData\\ClipboardMaster".to_string());
        
        AppSettings {
            max_items: 1000,
            keep_days: 90,
            max_image_size_mb: 10,
            compress_images: true,
            save_text: true,
            save_images: true,
            save_files: true,
            save_html: true,
            auto_cleanup: true,
            startup_delay_ms: 1000,
            database_path: format!("{}\\data\\clipboard.db", config_dir),
            cache_path: format!("{}\\cache", config_dir),
            hotkeys: HotkeyConfig {
                show_window: "Ctrl+Shift+V".to_string(),
                pin_item: "Ctrl+Shift+P".to_string(),
                search: "Ctrl+Shift+F".to_string(),
                next_item: "Ctrl+Down".to_string(),
                prev_item: "Ctrl+Up".to_string(),
            },
            ui: UiConfig {
                theme: "System".to_string(),
                opacity: 0.95,
                animation_speed: 200,
                show_preview: true,
                show_thumbnails: true,
                thumbnail_size: 64,
                font_size: 14,
            },
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Statistics {
    pub total_items: u32,
    pub text_items: u32,
    pub image_items: u32,
    pub file_items: u32,
    pub html_items: u32,
    pub favorite_items: u32,
    pub pinned_items: u32,
    pub total_size_bytes: u64,
    pub database_size_bytes: u64,
    pub cache_size_bytes: u64,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub enum ExportFormat {
    Json,
    Csv,
    Html,
    Markdown,
}

pub struct Database {
    conn: rusqlite::Connection,
}

impl Database {
    pub fn new(path: &str) -> Result<Self, Box<dyn std::error::Error>> {
        let parent = Path::new(path).parent()
            .ok_or("Invalid database path")?;
        std::fs::create_dir_all(parent)?;
        
        let conn = rusqlite::Connection::open(path)?;
        
        // 启用优化
        conn.pragma_update(None, "journal_mode", &"WAL")?;
        conn.pragma_update(None, "synchronous", &"NORMAL")?;
        conn.pragma_update(None, "foreign_keys", &"ON")?;
        conn.pragma_update(None, "cache_size", &"-2000")?; // 2MB cache
        
        // 创建表
        Self::create_tables(&conn)?;
        
        // 创建索引
        Self::create_indexes(&conn)?;
        
        Ok(Self { conn })
    }
    
    fn create_tables(conn: &rusqlite::Connection) -> Result<(), rusqlite::Error> {
        conn.execute_batch(
            r#"
            -- 主表
            CREATE TABLE IF NOT EXISTS clipboard_items (
                id TEXT PRIMARY KEY,
                content_type TEXT NOT NULL,
                content_data BLOB,
                content_json TEXT,
                timestamp INTEGER NOT NULL,
                tags_json TEXT DEFAULT '[]',
                favorite INTEGER DEFAULT 0,
                pinned INTEGER DEFAULT 0,
                source_app TEXT,
                source_window TEXT,
                preview_text TEXT,
                preview_image BLOB,
                metadata_json TEXT DEFAULT '{}',
                created_at INTEGER DEFAULT (strftime('%s', 'now')),
                updated_at INTEGER DEFAULT (strftime('%s', 'now')),
                access_count INTEGER DEFAULT 0
            );
            
            -- 标签表（用于快速搜索）
            CREATE TABLE IF NOT EXISTS item_tags (
                item_id TEXT NOT NULL,
                tag TEXT NOT NULL,
                created_at INTEGER DEFAULT (strftime('%s', 'now')),
                PRIMARY KEY (item_id, tag),
                FOREIGN KEY (item_id) REFERENCES clipboard_items(id) ON DELETE CASCADE
            );
            
            -- 元数据表
            CREATE TABLE IF NOT EXISTS item_metadata (
                item_id TEXT NOT NULL,
                key TEXT NOT NULL,
                value TEXT NOT NULL,
                PRIMARY KEY (item_id, key),
                FOREIGN KEY (item_id) REFERENCES clipboard_items(id) ON DELETE CASCADE
            );
            
            -- 搜索历史
            CREATE TABLE IF NOT EXISTS search_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                query TEXT NOT NULL,
                timestamp INTEGER DEFAULT (strftime('%s', 'now')),
                result_count INTEGER DEFAULT 0
            );
            "#
        )?;
        
        Ok(())
    }
    
    fn create_indexes(conn: &rusqlite::Connection) -> Result<(), rusqlite::Error> {
        conn.execute_batch(
            r#"
            CREATE INDEX IF NOT EXISTS idx_items_timestamp ON clipboard_items(timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_items_favorite ON clipboard_items(favorite) WHERE favorite = 1;
            CREATE INDEX IF NOT EXISTS idx_items_pinned ON clipboard_items(pinned) WHERE pinned = 1;
            CREATE INDEX IF NOT EXISTS idx_items_content_type ON clipboard_items(content_type);
            CREATE INDEX IF NOT EXISTS idx_items_preview ON clipboard_items(preview_text);
            CREATE INDEX IF NOT EXISTS idx_items_source ON clipboard_items(source_app);
            
            CREATE INDEX IF NOT EXISTS idx_tags_tag ON item_tags(tag);
            CREATE INDEX IF NOT EXISTS idx_tags_item ON item_tags(item_id);
            
            CREATE INDEX IF NOT EXISTS idx_metadata ON item_metadata(key, value);
            CREATE INDEX IF NOT EXISTS idx_search_history ON search_history(timestamp DESC);
            "#
        )?;
        
        Ok(())
    }
    
    pub fn save_item(&self, item: ClipboardItem) -> Result<(), Box<dyn std::error::Error>> {
        let tx = self.conn.transaction()?;
        
        // 检查是否已存在（基于内容哈希）
        let content_hash = Self::calculate_content_hash(&item.content);
        
        let exists: bool = tx.query_row(
            "SELECT 1 FROM clipboard_items WHERE preview_text = ? AND timestamp > ?",
            params![
                &item.preview_text,
                (Utc::now() - chrono::Duration::seconds(5)).timestamp()
            ],
            |row| row.get(0)
        ).unwrap_or(false);
        
        if exists {
            return Ok(());
        }
        
        // 准备数据
        let content_type = match item.content {
            ClipboardContent::Text(_) => "text",
            ClipboardContent::Image(_) => "image",
            ClipboardContent::FileList(_) => "file",
            ClipboardContent::Html(_) => "html",
            ClipboardContent::RichText(_) => "richtext",
            ClipboardContent::Custom(name, _) => &name,
        };
        
        let content_json = serde_json::to_string(&item.content)?;
        let tags_json = serde_json::to_string(&item.tags)?;
        let metadata_json = serde_json::to_string(&item.metadata)?;
        
        // 插入主记录
        tx.execute(
            r#"
            INSERT OR REPLACE INTO clipboard_items 
            (id, content_type, content_json, timestamp, tags_json, favorite, pinned, 
             source_app, source_window, preview_text, preview_image, metadata_json)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            "#,
            params![
                item.id.to_string(),
                content_type,
                content_json,
                item.timestamp.timestamp(),
                tags_json,
                item.favorite as i32,
                item.pinned as i32,
                item.source_app,
                item.source_window,
                item.preview_text,
                item.preview_image,
                metadata_json,
            ],
        )?;
        
        // 更新标签表
        tx.execute("DELETE FROM item_tags WHERE item_id = ?", params![item.id.to_string()])?;
        
        for tag in &item.tags {
            tx.execute(
                "INSERT INTO item_tags (item_id, tag) VALUES (?, ?)",
                params![item.id.to_string(), tag],
            )?;
        }
        
        // 更新元数据表
        tx.execute("DELETE FROM item_metadata WHERE item_id = ?", params![item.id.to_string()])?;
        
        for (key, value) in &item.metadata {
            tx.execute(
                "INSERT INTO item_metadata (item_id, key, value) VALUES (?, ?, ?)",
                params![item.id.to_string(), key, value],
            )?;
        }
        
        tx.commit()?;
        Ok(())
    }
    
    fn calculate_content_hash(content: &ClipboardContent) -> String {
        use sha2::{Sha256, Digest};
        let data = match content {
            ClipboardContent::Text(text) => text.as_bytes(),
            ClipboardContent::Html(html) => html.as_bytes(),
            ClipboardContent::RichText(rtf) => rtf.as_bytes(),
            ClipboardContent::Image(img) => &img.data,
            ClipboardContent::FileList(files) => {
                let paths: Vec<String> = files.iter()
                    .map(|f| f.path.to_string_lossy().to_string())
                    .collect();
                paths.join("|").as_bytes()
            }
            ClipboardContent::Custom(_, data) => data,
        };
        
        let mut hasher = Sha256::new();
        hasher.update(data);
        format!("{:x}", hasher.finalize())
    }
    
    pub fn get_recent_items(&self, limit: u32) -> Result<Vec<ClipboardItem>, Box<dyn std::error::Error>> {
        let mut stmt = self.conn.prepare(
            "SELECT * FROM clipboard_items ORDER BY timestamp DESC LIMIT ?"
        )?;
        
        let items = stmt.query_map(params![limit], |row| self.row_to_item(row))?;
        
        let mut result = Vec::new();
        for item in items {
            result.push(item?);
        }
        
        Ok(result)
    }
    
    fn row_to_item(&self, row: &rusqlite::Row) -> rusqlite::Result<ClipboardItem> {
        let id_str: String = row.get("id")?;
        let content_json: String = row.get("content_json")?;
        let tags_json: String = row.get("tags_json")?;
        let metadata_json: String = row.get("metadata_json")?;
        
        let id = Uuid::parse_str(&id_str).map_err(|e| {
            rusqlite::Error::FromSqlConversionFailure(0, rusqlite::types::Type::Text, Box::new(e))
        })?;
        
        let content: ClipboardContent = serde_json::from_str(&content_json)
            .map_err(|e| rusqlite::Error::FromSqlConversionFailure(
                0, rusqlite::types::Type::Text, Box::new(e)
            ))?;
        
        let tags: Vec<String> = serde_json::from_str(&tags_json)
            .map_err(|e| rusqlite::Error::FromSqlConversionFailure(
                0, rusqlite::types::Type::Text, Box::new(e)
            ))?;
        
        let metadata: HashMap<String, String> = serde_json::from_str(&metadata_json)
            .map_err(|e| rusqlite::Error::FromSqlConversionFailure(
                0, rusqlite::types::Type::Text, Box::new(e)
            ))?;
        
        Ok(ClipboardItem {
            id,
            content,
            timestamp: DateTime::from_timestamp(row.get("timestamp")?, 0)
                .unwrap_or_else(Utc::now),
            tags,
            favorite: row.get("favorite")?,
            pinned: row.get("pinned")?,
            source_app: row.get("source_app")?,
            source_window: row.get("source_window")?,
            preview_text: row.get("preview_text")?,
            preview_image: row.get("preview_image")?,
            metadata,
        })
    }
    
    pub fn cleanup_old_items(&self, keep_days: u32) -> Result<u32, Box<dyn std::error::Error>> {
        let cutoff = (Utc::now() - chrono::Duration::days(keep_days as i64)).timestamp();
        
        let count = self.conn.execute(
            r#"
            DELETE FROM clipboard_items 
            WHERE favorite = 0 AND pinned = 0 AND timestamp < ?
            "#,
            params![cutoff],
        )?;
        
        // 清理孤立数据
        self.conn.execute_batch(
            r#"
            DELETE FROM item_tags WHERE item_id NOT IN (SELECT id FROM clipboard_items);
            DELETE FROM item_metadata WHERE item_id NOT IN (SELECT id FROM clipboard_items);
            "#
        )?;
        
        // 清理缓存文件（需要在应用层实现）
        
        Ok(count as u32)
    }
    
    pub fn get_statistics(&self) -> Result<Statistics, Box<dyn std::error::Error>> {
        let mut stats = Statistics {
            total_items: 0,
            text_items: 0,
            image_items: 0,
            file_items: 0,
            html_items: 0,
            favorite_items: 0,
            pinned_items: 0,
            total_size_bytes: 0,
            database_size_bytes: 0,
            cache_size_bytes: 0,
        };
        
        // 获取数据库大小
        let db_size: i64 = self.conn.query_row(
            "SELECT page_count * page_size FROM pragma_page_count(), pragma_page_size()",
            [],
            |row| row.get(0),
        )?;
        stats.database_size_bytes = db_size as u64;
        
        // 获取各项统计
        stats.total_items = self.conn.query_row(
            "SELECT COUNT(*) FROM clipboard_items",
            [],
            |row| row.get(0),
        )?;
        
        stats.text_items = self.conn.query_row(
            "SELECT COUNT(*) FROM clipboard_items WHERE content_type = 'text'",
            [],
            |row| row.get(0),
        )?;
        
        stats.image_items = self.conn.query_row(
            "SELECT COUNT(*) FROM clipboard_items WHERE content_type = 'image'",
            [],
            |row| row.get(0),
        )?;
        
        stats.file_items = self.conn.query_row(
            "SELECT COUNT(*) FROM clipboard_items WHERE content_type = 'file'",
            [],
            |row| row.get(0),
        )?;
        
        stats.html_items = self.conn.query_row(
            "SELECT COUNT(*) FROM clipboard_items WHERE content_type = 'html'",
            [],
            |row| row.get(0),
        )?;
        
        stats.favorite_items = self.conn.query_row(
            "SELECT COUNT(*) FROM clipboard_items WHERE favorite = 1",
            [],
            |row| row.get(0),
        )?;
        
        stats.pinned_items = self.conn.query_row(
            "SELECT COUNT(*) FROM clipboard_items WHERE pinned = 1",
            [],
            |row| row.get(0),
        )?;
        
        // 估算总大小（文本长度 + 图片大小）
        let text_size: i64 = self.conn.query_row(
            "SELECT SUM(LENGTH(content_json)) FROM clipboard_items",
            [],
            |row| row.get(0),
        ).unwrap_or(0);
        
        let image_size: i64 = self.conn.query_row(
            "SELECT SUM(LENGTH(preview_image)) FROM clipboard_items WHERE preview_image IS NOT NULL",
            [],
            |row| row.get(0),
        ).unwrap_or(0);
        
        stats.total_size_bytes = (text_size + image_size) as u64;
        
        Ok(stats)
    }
}

pub struct ClipboardMonitor {
    settings: Arc<RwLock<AppSettings>>,
    database: Arc<Database>,
    event_tx: Sender<ClipboardEvent>,
    running: Arc<std::sync::atomic::AtomicBool>,
}

impl ClipboardMonitor {
    pub fn new(
        settings: Arc<RwLock<AppSettings>>,
        database: Arc<Database>,
        event_tx: Sender<ClipboardEvent>,
    ) -> Result<Self, Box<dyn std::error::Error>> {
        Ok(Self {
            settings,
            database,
            event_tx,
            running: Arc::new(std::sync::atomic::AtomicBool::new(false)),
        })
    }
    
    pub fn start(&self) -> Result<(), Box<dyn std::error::Error>> {
        self.running.store(true, std::sync::atomic::Ordering::SeqCst);
        
        let running = self.running.clone();
        let settings = self.settings.clone();
        let database = self.database.clone();
        let event_tx = self.event_tx.clone();
        
        std::thread::spawn(move || {
            if let Err(e) = Self::monitor_loop(running, settings, database, event_tx) {
                error!("Clipboard monitor error: {}", e);
            }
        });
        
        Ok(())
    }
    
    pub fn stop(&self) -> Result<(), Box<dyn std::error::Error>> {
        self.running.store(false, std::sync::atomic::Ordering::SeqCst);
        Ok(())
    }
    
    fn monitor_loop(
        running: Arc<std::sync::atomic::AtomicBool>,
        settings: Arc<RwLock<AppSettings>>,
        database: Arc<Database>,
        event_tx: Sender<ClipboardEvent>,
    ) -> Result<(), Box<dyn std::error::Error>> {
        use windows::Win32::UI::WindowsAndMessaging::*;
        use windows::Win32::System::DataExchange::*;
        
        // 创建隐藏窗口
        let hwnd = unsafe {
            let instance = GetModuleHandleW(None)?;
            let class_name = s!("ClipboardMasterWindow");
            
            let wc = WNDCLASSW {
                lpfnWndProc: Some(Self::window_proc),
                hInstance: instance,
                lpszClassName: class_name,
                ..Default::default()
            };
            
            RegisterClassW(&wc)?;
            
            CreateWindowExW(
                Default::default(),
                class_name,
                s!("Clipboard Master"),
                WS_OVERLAPPEDWINDOW,
                0, 0, 0, 0,
                None,
                None,
                instance,
                None,
            )?
        };
        
        // 注册剪贴板监听
        unsafe {
            AddClipboardFormatListener(hwnd)?;
        }
        
        // 消息循环
        let mut msg = MSG::default();
        while running.load(std::sync::atomic::Ordering::SeqCst) {
            unsafe {
                if PeekMessageW(&mut msg, hwnd, 0, 0, PM_REMOVE).as_bool() {
                    if msg.message == WM_CLIPBOARDUPDATE {
                        if let Ok(item) = Self::capture_clipboard_content(&settings) {
                            if let Err(e) = database.save_item(item.clone()) {
                                error!("Failed to save clipboard item: {}", e);
                            } else {
                                let _ = event_tx.send(ClipboardEvent::ItemAdded(item));
                            }
                        }
                    }
                    TranslateMessage(&msg);
                    DispatchMessageW(&msg);
                } else {
                    std::thread::sleep(Duration::from_millis(10));
                }
            }
        }
        
        // 清理
        unsafe {
            RemoveClipboardFormatListener(hwnd)?;
            DestroyWindow(hwnd)?;
        }
        
        Ok(())
    }
    
    extern "system" fn window_proc(
        hwnd: HWND,
        msg: u32,
        wparam: WPARAM,
        lparam: LPARAM,
    ) -> LRESULT {
        unsafe {
            DefWindowProcW(hwnd, msg, wparam, lparam)
        }
    }
    
    fn capture_clipboard_content(
        settings: &Arc<RwLock<AppSettings>>
    ) -> Result<ClipboardItem, Box<dyn std::error::Error>> {
        use windows::Win32::UI::WindowsAndMessaging::*;
        use windows::Win32::System::DataExchange::*;
        use windows::Win32::Graphics::Gdi::*;
        
        unsafe {
            if !OpenClipboard(None).as_bool() {
                return Err("无法打开剪贴板".into());
            }
            
            let mut item = ClipboardItem {
                id: Uuid::new_v4(),
                content: ClipboardContent::Text("".to_string()),
                timestamp: Utc::now(),
                tags: Vec::new(),
                favorite: false,
                pinned: false,
                source_app: Self::get_foreground_app(),
                source_window: Self::get_foreground_window(),
                preview_text: String::new(),
                preview_image: None,
                metadata: HashMap::new(),
            };
            
            // 检查各种格式
            if IsClipboardFormatAvailable(CF_UNICODETEXT as u32).as_bool() {
                item = Self::capture_text(item)?;
            } else if IsClipboardFormatAvailable(CF_BITMAP as u32).as_bool() {
                item = Self::capture_image(item, settings)?;
            } else if IsClipboardFormatAvailable(CF_HDROP as u32).as_bool() {
                item = Self::capture_files(item)?;
            } else if IsClipboardFormatAvailable(Self::register_format("HTML Format")?).as_bool() {
                item = Self::capture_html(item)?;
            }
            
            CloseClipboard();
            Ok(item)
        }
    }
    
    fn capture_text(mut item: ClipboardItem) -> Result<ClipboardItem, Box<dyn std::error::Error>> {
        unsafe {
            let h_mem = GetClipboardData(CF_UNICODETEXT as u32)?;
            let ptr = GlobalLock(h_mem)? as *const u16;
            
            let mut len = 0;
            while *ptr.add(len) != 0 {
                len += 1;
            }
            
            let text = String::from_utf16_lossy(std::slice::from_raw_parts(ptr, len));
            GlobalUnlock(h_mem);
            
            item.content = ClipboardContent::Text(text.clone());
            item.preview_text = if text.len() > 100 {
                format!("{}...", &text[..100])
            } else {
                text
            };
            
            Ok(item)
        }
    }
    
    fn capture_image(
        mut item: ClipboardItem,
        settings: &Arc<RwLock<AppSettings>>
    ) -> Result<ClipboardItem, Box<dyn std::error::Error>> {
        use image::{ImageBuffer, Rgba};
        
        unsafe {
            let h_bitmap = GetClipboardData(CF_BITMAP as u32)? as HBITMAP;
            
            // 获取位图信息
            let mut bmp = BITMAP::default();
            GetObjectW(
                h_bitmap,
                std::mem::size_of::<BITMAP>() as i32,
                &mut bmp as *mut _ as *mut std::ffi::c_void,
            );
            
            // 创建图像缓冲区
            let width = bmp.bmWidth as u32;
            let height = bmp.bmHeight as u32;
            let bits_ptr = bmp.bmBits as *const u8;
            let bits_len = (width * height * 4) as usize;
            
            let slice = std::slice::from_raw_parts(bits_ptr, bits_len);
            let img_buffer = ImageBuffer::<Rgba<u8>, _>::from_raw(width, height, slice)
                .ok_or("无法创建图像缓冲区")?;
            
            // 转换为PNG
            let mut png_data = Vec::new();
            img_buffer.write_to(
                &mut std::io::Cursor::new(&mut png_data),
                image::ImageFormat::Png,
            )?;
            
            // 创建缩略图
            let thumbnail = if settings.read().compress_images {
                let thumb_img = image::imageops::thumbnail(&img_buffer, 128, 128);
                let mut thumb_data = Vec::new();
                thumb_img.write_to(
                    &mut std::io::Cursor::new(&mut thumb_data),
                    image::ImageFormat::Png,
                )?;
                Some(thumb_data)
            } else {
                None
            };
            
            item.content = ClipboardContent::Image(ImageData {
                data: png_data,
                width,
                height,
                format: ImageFormat::Png,
                thumbnail: thumbnail.unwrap_or_default(),
            });
            
            item.preview_text = format!("[Image {}x{}]", width, height);
            item.preview_image = thumbnail;
            
            Ok(item)
        }
    }
    
    fn register_format(format_name: &str) -> Result<u32, Box<dyn std::error::Error>> {
        use windows::Win32::System::DataExchange::*;
        
        unsafe {
            let name_wide: Vec<u16> = format_name.encode_utf16().chain(std::iter::once(0)).collect();
            let format = RegisterClipboardFormatW(PCWSTR(name_wide.as_ptr()))?;
            Ok(format as u32)
        }
    }
    
    fn get_foreground_app() -> Option<String> {
        use windows::Win32::UI::WindowsAndMessaging::*;
        
        unsafe {
            let hwnd = GetForegroundWindow();
            let mut text: [u16; 256] = [0; 256];
            let len = GetWindowTextW(hwnd, &mut text);
            
            if len > 0 {
                Some(String::from_utf16_lossy(&text[..len as usize]))
            } else {
                None
            }
        }
    }
    
    fn get_foreground_window() -> Option<String> {
        use windows::Win32::UI::WindowsAndMessaging::*;
        use windows::Win32::Foundation::HWND;
        
        unsafe {
            let hwnd = GetForegroundWindow();
            let mut class_name: [u16; 256] = [0; 256];
            let len = GetClassNameW(hwnd, &mut class_name);
            
            if len > 0 {
                Some(String::from_utf16_lossy(&class_name[..len as usize]))
            } else {
                None
            }
        }
    }
}