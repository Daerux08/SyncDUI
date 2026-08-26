#!/usr/bin/env bash
set -e

APP_NAME="syncdui"
DISPLAY_NAME="SyncDUI"
APP_VERSION="0.0.1"
ARCH="amd64"

PROJECT_PATH="./SyncDUI.csproj"
PUBLISH_DIR="./publish"
DEB_DIR="./Package"
DEBIAN_DIR="$DEB_DIR/DEBIAN"
USR_BIN_DIR="$DEB_DIR/usr/bin"
APP_SHARE_DIR="$DEB_DIR/usr/share/$APP_NAME"
ICON_DIR="$DEB_DIR/usr/share/icons/hicolor/256x256/apps"

echo "==> Cleaning old builds"
rm -rf "$PUBLISH_DIR"
rm -rf "$DEB_DIR"
rm -f *.deb

echo "==> Publishing Avalonia app (AOT)"
dotnet publish "$PROJECT_PATH" -c Release -r linux-x64 /p:PublishAot=true -o "$PUBLISH_DIR"

echo "==> Creating Debian package structure"
mkdir -p "$DEBIAN_DIR"
mkdir -p "$USR_BIN_DIR"
mkdir -p "$APP_SHARE_DIR"
mkdir -p "$DEB_DIR/usr/share/applications"
mkdir -p "$ICON_DIR"

echo "==> Copying all published files and native assets"
cp -r "$PUBLISH_DIR"/* "$APP_SHARE_DIR/"

echo "==> Creating symlinks for commands"
ln -sf "/usr/share/$APP_NAME/SyncDUI" "$USR_BIN_DIR/$APP_NAME"
ln -sf "/usr/share/$APP_NAME/SyncDUI" "$USR_BIN_DIR/syncdui"

echo "==> Copying icon"
ICON_SOURCE="./Assets/syncthingtray_222729.avif"
ICON_TARGET="$ICON_DIR/$APP_NAME.png"
if [ -f "$ICON_SOURCE" ]; then
    if command -v magick >/dev/null 2>&1; then
        magick "$ICON_SOURCE" -resize 256x256 "$ICON_TARGET"
    elif command -v convert >/dev/null 2>&1; then
        convert "$ICON_SOURCE" -resize 256x256 "$ICON_TARGET"
    else
        cp "$ICON_SOURCE" "$ICON_TARGET"
        echo "Warning: no image converter found; copied the source asset directly. Install ImageMagick for a valid PNG icon."
    fi
fi

echo "==> Creating .desktop file"
cat <<DESKTOP > "$DEB_DIR/usr/share/applications/$APP_NAME.desktop"
[Desktop Entry]
Version=1.0
Type=Application
Name=$DISPLAY_NAME
GenericName=System Utilities
Comment=Desktop SyncThing app
Exec=syncdui
TryExec=syncdui
Icon=$APP_NAME
Terminal=false
Categories=Network;FileTransfer;System;Settings;
StartupNotify=true
StartupWMClass=SyncDUI
Keywords=Sync;Thing;Share;File;SyncDUI;
DESKTOP

echo "==> Creating control file"
cat <<CONTROL > "$DEB_DIR/DEBIAN/control"
Package: $APP_NAME
Version: $APP_VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Depends: syncthing
Maintainer: Alexander Kozlov
Description: Desktop UI for SyncThing
CONTROL

echo "==> Creating post-installation scripts"
cat <<POSTINST > "$DEB_DIR/DEBIAN/postinst"
#!/bin/sh
set -e
if [ "\$1" = "configure" ]; then
    update-desktop-database -q /usr/share/applications || true
    gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
fi
POSTINST

cat <<POSTRM > "$DEB_DIR/DEBIAN/postrm"
#!/bin/sh
set -e
if [ "\$1" = "remove" ] || [ "\$1" = "purge" ]; then
    update-desktop-database -q /usr/share/applications || true
    gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
fi
POSTRM

echo "==> Finalizing permissions"
find "$DEB_DIR" -type d -exec chmod 755 {} +
find "$DEB_DIR" -type f -exec chmod 644 {} +
chmod 755 "$APP_SHARE_DIR/SyncDUI"
chmod 755 "$DEB_DIR/DEBIAN/postinst"
chmod 755 "$DEB_DIR/DEBIAN/postrm"

echo "==> Building .deb package"
OUTPUT_FILE="${APP_NAME}_${APP_VERSION}_${ARCH}.deb"
dpkg-deb --root-owner-group --build "$DEB_DIR" "$OUTPUT_FILE"

echo "==> Done!"
echo "Created package: $OUTPUT_FILE"
