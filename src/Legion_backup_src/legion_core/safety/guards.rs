use std::sync::atomic::{AtomicBool, Ordering};

// Global static override for write operations. Defaults to FALSE (Safe/Read-only).
static WRITE_ENABLED: AtomicBool = AtomicBool::new(false);

pub struct GlobalWriteLock;

impl GlobalWriteLock {
    /// Request permission to perform write operations.
    /// This should be called when the user explicitly provides a --force or --set flag.
    pub fn request_write_access() {
        WRITE_ENABLED.store(true, Ordering::SeqCst);
    }

    /// Check if write operations are currently allowed.
    pub fn is_write_allowed() -> bool {
        WRITE_ENABLED.load(Ordering::SeqCst)
    }

    /// Revoke write access (lock down).
    pub fn revoke_access() {
        WRITE_ENABLED.store(false, Ordering::SeqCst);
    }
}
