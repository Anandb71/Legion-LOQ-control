fn main() {
    // Embed manifest for admin elevation on Windows
    #[cfg(windows)]
    {
        if std::env::var("CARGO_CFG_TARGET_OS").unwrap_or_default() == "windows" {
            embed_resource::compile("legion-loq-control.exe.manifest", embed_resource::NONE);
        }
    }
}
