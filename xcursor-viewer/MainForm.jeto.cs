using System;
using Eto.Forms;
using Eto.Drawing;
using Eto.Serialization.Json;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace xcursor_viewer;

public class MainForm : Form {
    protected Drawable Canvas;
    protected Splitter MainSplitter;
    protected TreeGridView TreeGridViewFolders;
    private XCursor selectedCursor;
    private (int Index, long LastUpdate)[] frames = [];
    private bool showAllFiles = true;

    private Bitmap driveIcon = Bitmap.FromResource("xcursor_viewer.Resources.drive-icon.png");
    private Bitmap folderIcon = Bitmap.FromResource("xcursor_viewer.Resources.folder-icon.png");
    private Bitmap fileIcon = Bitmap.FromResource("xcursor_viewer.Resources.file-icon.png");

    private class FSItem : TreeGridItem {
        public string Name { get; set; }
        public string Path { get; }
        public XCursor Cursor { get; }
        public Bitmap Icon { get; }

        public FSItem() { }

        public FSItem(string name, string path, bool isFile, Bitmap icon = null) {
            Name = name;
            Path = path;

            if(isFile && File.Exists(path) && XCursor.IsXCursor(path)) {
                Cursor = new XCursor(path);
                Icon = Cursor.Images.First().First();
            } else {
                Cursor = null;
                Icon = icon;

                if(!IsEmpty(path)) base.Children.Add(new FSItem());
            }

            //base.Values = [name, path];
        }

        public static bool IsEmpty(string path) {
            try {
                DirectoryInfo dir = new(path);
                if(dir.GetDirectories("*", SearchOption.TopDirectoryOnly).Length > 0 || dir.GetFiles("*", SearchOption.TopDirectoryOnly).Length > 0) {
                    return false;
                }
            } catch { }
            return true;
        }

        public override string ToString() {
            return Name;
        }
    }

    public MainForm() {
        JsonReader.Load(this);

        Canvas.Paint += (sender, e) => {
            var g = e.Graphics;

            g.Clear(Color.FromArgb(0x1c, 0x1e, 0x1f));

            var currentTime = DateTime.UtcNow.Ticks / 10_000;

            if(selectedCursor != null) {
                int p = this.Padding.Left;
                int cx = p;
                int cy = p;
                for(int i = 0; i < selectedCursor.Images.Count; i++) {
                    Bitmap frame = selectedCursor.Images[i][frames[i].Index];

                    if(cx + frame.Width >= Canvas.Width - p) {
                        cx = p;
                        cy += frame.Height + p;
                    }
                    g.DrawImage(frame, cx, cy);
                    cx += frame.Width + p;

                    if(selectedCursor.Images[i].Count > 1) {
                        if(frames[i].LastUpdate == 0) {
                            frames[i].LastUpdate = currentTime;
                        } else if(currentTime - frames[i].LastUpdate >= selectedCursor.ImagesChunks[i].Delay) {
                            Debug.WriteLine(currentTime - frames[i].LastUpdate);
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

        DriveInfo[] drives = DriveInfo.GetDrives();
        foreach(DriveInfo drive in drives) {
            FSItem item = new FSItem(drive.Name, drive.Name, false, driveIcon);
            PopulateTreeGridItem(drive.Name, item);
            treeGridItems.Add(item);
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

        foreach(DirectoryInfo subDir in dir.GetDirectories()) {
            if(!subDir.Attributes.HasFlag(FileAttributes.Hidden)) {
                FSItem subItem = new(subDir.Name, subDir.FullName, false, folderIcon);
                item.Children.Add(subItem);
            }
        }

        foreach(FileInfo file in dir.GetFiles()) {
            if(!file.Attributes.HasFlag(FileAttributes.Hidden)) {
                FSItem subItem = new(file.Name, file.FullName, true, fileIcon);
                if(showAllFiles || subItem.Cursor != null) {
                    item.Children.Add(subItem);
                }
            }
        }
    }
}