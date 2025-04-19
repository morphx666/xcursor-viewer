using System;
using Eto.Forms;
using Eto.Drawing;
using Eto.Serialization.Json;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace xcursor_viewer;

public partial class MainForm : Form {
    protected Drawable Canvas;
    protected Splitter MainSplitter;
    protected TreeGridView TreeGridViewFolders;
    private XCursor selectedCursor;
    private (int Index, long LastUpdate)[] frames = [];
    private bool showAllFiles = true;
    private float zoom = 1.0f;

    private Bitmap driveIcon;
    private Bitmap folderIcon;
    private Bitmap fileIcon;

    public MainForm() {
        JsonReader.Load(this);

        string theme = Environment.OSVersion.Platform == PlatformID.Win32NT ? "black" : "white";
        driveIcon = Bitmap.FromResource($"xcursor_viewer.Resources.drive-icon-{theme}.png");
        folderIcon = Bitmap.FromResource($"xcursor_viewer.Resources.folder-icon-{theme}.png");
        fileIcon = Bitmap.FromResource($"xcursor_viewer.Resources.file-icon-{theme}.png");

        Canvas.MouseWheel += (sender, e) => {
            if(e.Delta.Height > 0) {
                zoom += 0.1f;
                if(zoom > 10) zoom = 10;
            } else if(e.Delta.Height < 0) {
                zoom -= 0.1f;
                if(zoom < 0.1f) zoom = 0.1f;
            }
        };

        Canvas.Paint += (sender, e) => {
            var g = e.Graphics;

            g.Clear(Color.FromArgb(0x1c, 0x1e, 0x1f));
            g.ScaleTransform(zoom, zoom);

            var currentTime = DateTime.UtcNow.Ticks / 10_000;

            if(selectedCursor != null) {
                int p = this.Padding.Left;
                int cx = p;
                int cy = p;
                for(int i = 0; i < selectedCursor.Images.Count; i++) {
                    Bitmap frame = selectedCursor.Images[i][frames[i].Index];

                    if(cx + frame.Width >= (Canvas.Width - p) / zoom) {
                        cx = p;
                        cy += frame.Height + p;
                    }
                    g.DrawImage(frame, cx, cy);
                    cx += frame.Width + p;

                    if(selectedCursor.Images[i].Count > 1) {
                        if(frames[i].LastUpdate == 0) {
                            frames[i].LastUpdate = currentTime;
                        } else if(currentTime - frames[i].LastUpdate >= selectedCursor.ImagesChunks[i].Delay) {
                            frames[i].LastUpdate = currentTime;
                            frames[i].Index++;
                            if(frames[i].Index >= selectedCursor.Images[i].Count) frames[i].Index = 0;
                        }
                    }
                }
            }
        };

        Task.Run(async () => {
            try {
                while(true) {
                    await Task.Delay(1);
                    Application.Instance.Invoke(Canvas.Invalidate);
                }
            } catch { }
        });

        this.Icon = Icon.FromResource("xcursor_viewer.Resources.app-icon.png");

        TreeGridItemCollection treeGridItems = [];
        ImageTextCell imageTextCell = new("Icon", "Name");

        TreeGridViewFolders.Columns.Add(new GridColumn {
            DataCell = imageTextCell,
            HeaderText = "Folders",
            Editable = false,
            Sortable = true,
            AutoSize = true,
        });
        TreeGridViewFolders.Columns.Add(new GridColumn {
            DataCell = new TextBoxCell("ImagesCountAsString"),
            HeaderText = "Images",
            Editable = false,
            Sortable = true,
            AutoSize = true,
        });
        TreeGridViewFolders.Columns.Add(new GridColumn {
            DataCell = new TextBoxCell("HasAnimationsAsString"),
            HeaderText = "Animations",
            Editable = false,
            Sortable = true,
            AutoSize = true,
        });

        if(Environment.OSVersion.Platform != PlatformID.Win32NT) {
            treeGridItems.Add(new FSItem("/", "", false, driveIcon));
            treeGridItems.Add(new FSItem(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "~", false, driveIcon));
        }

        DriveInfo[] drives = DriveInfo.GetDrives();
        foreach(DriveInfo drive in drives) {
            if(Environment.OSVersion.Platform == PlatformID.Win32NT) {
                if(drive.DriveType != DriveType.Unknown && drive.DriveType != DriveType.NoRootDirectory) {
                    FSItem item = new($"{drive.Name} {drive.VolumeLabel}", drive.Name, false, driveIcon);
                    treeGridItems.Add(item);
                }
            } else {
                if(drive.DriveType == DriveType.Removable || drive.DriveType == DriveType.Network || drive.DriveType == DriveType.CDRom) {
                    FSItem item = new($"{drive.Name} {drive.VolumeLabel}", drive.Name, false, driveIcon);
                    treeGridItems.Add(item);
                }
            }
        }

        TreeGridViewFolders.DataStore = treeGridItems;

        TreeGridViewFolders.Expanding += (sender, e) => {
            FSItem item = (FSItem)e.Item;

            if(((FSItem)item.Children[0]).Name == null) {
                PopulateTreeGridItem(item.Path, item);
                item.Children.RemoveAt(0);
            }
        };

        TreeGridViewFolders.SelectedItemChanged += (sender, e) => {
            FSItem item = (FSItem)TreeGridViewFolders.SelectedItem;
            if(item == null) return;

            if(item.Cursor != null) {
                frames = new (int Index, long LastUpdate)[item.Cursor.Images.Count];
            } else {
                frames = [];
            }
            selectedCursor = item.Cursor;
        };
    }

    private void PopulateTreeGridItem(string path, FSItem item) {
        DirectoryInfo dir = new(path);

        EnumerationOptions options = new() {
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
        };

        foreach(DirectoryInfo subDir in dir.GetDirectories("*", options).OrderBy(d => d.Name)) {
            if(!subDir.Attributes.HasFlag(FileAttributes.Hidden)) {
                FSItem subItem = new(subDir.Name, subDir.FullName, false, folderIcon);
                item.Children.Add(subItem);
            }
        }

        foreach(FileInfo file in dir.GetFiles("*", options).OrderBy(f => f.Name)) {
            if(!file.Attributes.HasFlag(FileAttributes.Hidden)) {
                FSItem subItem = new(file.Name, file.FullName, true, fileIcon);
                if(showAllFiles || subItem.Cursor != null) {
                    item.Children.Add(subItem);
                }
            }
        }
    }
}