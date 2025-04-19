using System;
using Eto.Forms;
using Eto.Drawing;
using Eto.Serialization.Json;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace xcursor_viewer;

public partial class MainForm : Form {
    protected Drawable Canvas;
    protected Splitter MainSplitter;
    protected TreeGridView TreeGridViewFolders;
    private XCursor selectedCursor;
    private (int Index, long LastUpdate)[] frames = [];
    private bool showAllFiles = true;
    private float zoom = 1.0f;

    private Bitmap driveIcon = Bitmap.FromResource("xcursor_viewer.Resources.drive-icon.png");
    private Bitmap folderIcon = Bitmap.FromResource("xcursor_viewer.Resources.folder-icon.png");
    private Bitmap fileIcon = Bitmap.FromResource("xcursor_viewer.Resources.file-icon.png");

    public MainForm() {
        JsonReader.Load(this);

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
            while(true) {
                await Task.Delay(1);
                Application.Instance.Invoke(Canvas.Invalidate);
            }
        });

        this.Icon = Icon.FromResource("xcursor_viewer.Resources.cursor-icon.png");

        TreeGridItemCollection treeGridItems = [];
        TreeGridViewFolders.Columns.Add(new GridColumn {
            DataCell = new ImageTextCell("Icon", "Name"),
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

        DriveInfo[] drives = DriveInfo.GetDrives();
        foreach(DriveInfo drive in drives) {
            if(drive.DriveType != DriveType.Unknown && drive.DriveType != DriveType.NoRootDirectory) {
                FSItem item = new($"{drive.Name} {drive.VolumeLabel}", drive.Name, false, driveIcon);
                treeGridItems.Add(item);
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