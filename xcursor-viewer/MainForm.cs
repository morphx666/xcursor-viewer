using System;
using Eto.Forms;
using Eto.Drawing;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace xcursor_viewer;

partial class MainForm : Form, INotifyPropertyChanged {
    private readonly List<XCursor> selectedCursors = [];
    private (int Index, long LastUpdate)[] frames = [];
    private float zoom = 1.0f;
    private bool isCtrlDown = false;
    private readonly Font cursorNameFont;

    private const int MAX_CURSORS_PER_FILE = 1_000;

    private Color canvasBackgroundColor = Color.FromArgb(0x1c, 0x1e, 0x1f);

    private readonly Bitmap driveIcon;
    private readonly Bitmap folderIcon;
    private readonly Bitmap fileIcon;

    private bool showAllFiles = false;
    private bool darkMode = true;

    private bool ShowAllFiles {
        get => showAllFiles;
        set {
            showAllFiles = value;

            List<string> itemsWithChildren = [];
            itemsWithChildren.AddRange(treeGridItems.GetItemsWithChildren());
            itemsWithChildren = [.. itemsWithChildren.OrderBy(i => i.Split(Path.DirectorySeparatorChar).Length)];

            foreach(string itemPath in itemsWithChildren) {
                FSItem item = treeGridItems.FindItemByPath(itemPath);
                item.Children.Clear();
                PopulateTreeGridItem(item.Path, item);
                item.Expanded = true;
            }
            TreeGridViewFolders.ReloadData();

            OnPropertyChanged();
        }
    }
    private bool DarkMode {
        get => darkMode;
        set {
            darkMode = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public MainForm(string[] args) {
        InitializeComponent();

        cursorNameFont = new Font(FontFamilies.Sans, 12, FontStyle.Bold);

        string appTheme = Environment.OSVersion.Platform == PlatformID.Win32NT ? "black" : "white";
        driveIcon = Bitmap.FromResource($"xcursor_viewer.Resources.drive-icon-{appTheme}.png");
        folderIcon = Bitmap.FromResource($"xcursor_viewer.Resources.folder-icon-{appTheme}.png");
        fileIcon = Bitmap.FromResource($"xcursor_viewer.Resources.file-icon-{appTheme}.png");

        PopulateDrives(treeGridItems);
        TreeGridViewFolders.DataStore = treeGridItems;

        SetupEventHandlers();
        this.Shown += (_, _) => ParseCommandLine(args);

        Task.Run(async () => {
            try {
                while(true) {
                    await Task.Delay(1);
                    Application.Instance.Invoke(Canvas.Invalidate);
                }
            } catch { }
        });
    }

    private void ParseCommandLine(string[] args) {
        if(true || args.Length > 0) {
            string path = args[0].TrimEnd(Path.DirectorySeparatorChar);
            bool isFile = File.Exists(path);
            bool isDirectory = Directory.Exists(path);
            if(isFile || isDirectory) {
                string[] tokens = path.Split(Path.DirectorySeparatorChar);
                if(tokens[0] == "") tokens[0] = Path.DirectorySeparatorChar.ToString(); // Fix for unix-like paths
                string currentPath = "";
                foreach(string token in tokens) {
                    currentPath = Path.Combine(currentPath, token);
                    if(isFile && currentPath == path) break;

                    FSItem item = treeGridItems.FindItemByPath(currentPath);
                    item.Children.Clear();
                    PopulateTreeGridItem(item.Path, item);
                    item.Expanded = true;
                }
                FSItem selItem = treeGridItems.FindItemByPath(path);
                TreeGridViewFolders.SelectedItem = selItem;
                TreeGridViewFolders.ReloadData();
                TreeGridViewFolders.ScrollToRow(TreeGridViewFolders.SelectedRow);
            } else {
                MessageBox.Show(this, $"The path '{path}' does not exist.", "File or Path not found", MessageBoxButtons.OK, MessageBoxType.Error);
            }
        }
    }

    private void SetupEventHandlers() {
        KeyDown += (sender, e) => { if(e.Key == Keys.LeftControl) isCtrlDown = true; };
        KeyUp += (sender, e) => { if(e.Key == Keys.LeftControl) isCtrlDown = false; };

        Canvas.MouseWheel += (sender, e) => {
            if(!isCtrlDown) return;

            if(e.Delta.Height > 0) {
                zoom -= 0.1f;
                if(zoom > 10) zoom = 10;
            } else if(e.Delta.Height < 0) {
                zoom += 0.1f;
                if(zoom < 0.1f) zoom = 0.1f;
            }
        };

        Canvas.Paint += (sender, e) => RenderCursors(e);

        TreeGridViewFolders.CellDoubleClick += (sender, e) => {
            FSItem item = (FSItem)e.Item;
            if(item != null && item.Expandable) {
                item.Expanded = !item.Expanded;
                if(item.Expanded) PopulateItemChildren(item);
            }
        };

        TreeGridViewFolders.Expanding += (sender, e) => {
            FSItem item = (FSItem)e.Item;
            PopulateItemChildren(item);
        };

        TreeGridViewFolders.SelectedItemChanged += (sender, e) => {
            selectedCursors.Clear();
            frames = [];
            scrollableContainer.ScrollPosition = Point.Empty;

            FSItem item = (FSItem)TreeGridViewFolders.SelectedItem;
            if(item == null) return;

            if(item.IsFile) {
                if(item.Cursor != null) {
                    frames = new (int Index, long LastUpdate)[item.Cursor.Images.Count];
                    selectedCursors.Add(item.Cursor);
                }
            } else {
                int framesCount = 0;
                foreach(FSItem subItem in item.Children) {
                    if(subItem.IsFile && subItem.Cursor != null) {
                        framesCount += subItem.Cursor.Images.Count;
                        selectedCursors.Add(subItem.Cursor);
                    }
                }
                frames = new (int Index, long LastUpdate)[framesCount * MAX_CURSORS_PER_FILE];
            }
        };
    }

    private void PopulateItemChildren(FSItem item) {
        if(((FSItem)item.Children[0]).Name == null) {
            PopulateTreeGridItem(item.Path, item);
            item.Children.RemoveAt(0);

            TreeGridViewFolders.SelectedItem = null; // Force SelectedItemChanged event
            TreeGridViewFolders.SelectedItem = item;
        }
    }
    private void RenderCursors(PaintEventArgs e) {
        var g = e.Graphics;

        g.Clear(canvasBackgroundColor);
        g.ScaleTransform(zoom, zoom);

        var currentTime = DateTime.UtcNow.Ticks / 10_000;

        if(selectedCursors.Count > 0) {
            int p = this.Padding.Left;
            int cx = p;
            int cy = p;
            int bottomMost = 0;
            int rightMost = 0;
            for(int j = 0; j < selectedCursors.Count; j++) {
                XCursor selectedCursor = selectedCursors[j];
                int maxFrameHeight = 0;

                g.DrawText(cursorNameFont, darkMode ? Brushes.White : Brushes.Black, new PointF(cx, cy), selectedCursor.Name);
                cy += (int)(cursorNameFont.LineHeight * 1.1);

                for(int i = 0; i < selectedCursor.Images.Count; i++) {
                    if(selectedCursor.Images[i].Count == 0) continue;

                    int frameIndex = i + j * MAX_CURSORS_PER_FILE;
                    Bitmap frame = selectedCursor.Images[i][frames[frameIndex].Index];

                    rightMost = Math.Max(rightMost, cx + frame.Width + p);
                    bottomMost = Math.Max(bottomMost, cy + frame.Height + p);
                    maxFrameHeight = Math.Max(maxFrameHeight, frame.Height);

                    g.DrawImage(frame, cx, cy);
                    int widest = selectedCursor.Images[i].Max(f => f.Width);
                    cx += widest + p;

                    if(selectedCursor.Images[i].Count > 1) {
                        if(frames[frameIndex].LastUpdate == 0) {
                            frames[frameIndex].LastUpdate = currentTime;
                        } else if(currentTime - frames[frameIndex].LastUpdate >= selectedCursor.ImagesChunks[i].Delay) {
                            frames[frameIndex].LastUpdate = currentTime;
                            frames[frameIndex].Index++;
                            if(frames[frameIndex].Index >= selectedCursor.Images[i].Count) frames[frameIndex].Index = 0;
                        }
                    }
                }
                cx = p;
                cy += maxFrameHeight + p;
            }

            rightMost = (int)(rightMost * zoom);
            bottomMost = (int)(bottomMost * zoom);
            if(Canvas.Width != rightMost || Canvas.Height != bottomMost) {
                Canvas.Width = rightMost;
                Canvas.Height = bottomMost;
            }
        }
    }

    private void PopulateDrives(TreeGridItemCollection treeGridItems) {
        var AddDrive = (DriveInfo drive) => {
            string title = "";
            if(drive.Name == drive.VolumeLabel || drive.VolumeLabel == "") {
                title = drive.Name;
            } else {
                title = $"{drive.Name} ({drive.VolumeLabel})";
            }
            FSItem item = new(title, drive.Name, false, driveIcon);
            treeGridItems.Add(item);
        };

        DriveInfo[] drives = DriveInfo.GetDrives();
        if(Environment.OSVersion.Platform == PlatformID.Win32NT) {
            foreach(DriveInfo drive in drives) {
                if(drive.DriveType != DriveType.Unknown
                    && drive.DriveType != DriveType.NoRootDirectory
                    && drive.DriveType != DriveType.Ram) {
                    AddDrive(drive);
                }
            }
        } else {
            treeGridItems.Add(new FSItem("/", "/", false, driveIcon));
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            treeGridItems.Add(new FSItem(homeDir, homeDir, false, driveIcon));

            foreach(DriveInfo drive in drives) {
                if(drive.DriveType == DriveType.Removable
                    || drive.DriveType == DriveType.Network
                    || drive.DriveType == DriveType.CDRom) {
                    AddDrive(drive);
                }
            }
        }
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
                if(ShowAllFiles || subItem.Cursor != null) {
                    item.Children.Add(subItem);
                }
            }
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string name = null) {
        if(darkMode) {
            canvasBackgroundColor = Color.FromArgb(0x1c, 0x1e, 0x1f);
        } else {
            canvasBackgroundColor = Color.FromArgb(0xff, 0xff, 0xff);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
