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

partial class MainForm : Form {
    private Drawable Canvas;
    private TreeGridView TreeGridViewFolders;
    private TreeGridItemCollection treeGridItems = [];
    private Scrollable scrollableContainer;

    void InitializeComponent() {
        Title = "XCursor Viewer";
        ClientSize = new Size(980, 600);
        MinimumSize = new Size(800, 600);
        Padding = new Padding(10);
        Icon = Icon.FromResource("xcursor_viewer.Resources.app-icon.png");

        ImageTextCell imageTextCell = new("Icon", "Name");

        TreeGridViewFolders = new TreeGridView {
            ShowHeader = false,
            AllowMultipleSelection = false,
            AllowEmptySelection = true,

            Columns = {
                new GridColumn {
                    DataCell = imageTextCell,
                    HeaderText = "Folders",
                    Editable = false,
                    Sortable = true,
                    AutoSize = true,
                },
                new GridColumn {
                    DataCell = new TextBoxCell("ImagesCountAsString"),
                    HeaderText = "Images",
                    Editable = false,
                    Sortable = true,
                    AutoSize = true,
                },
                new GridColumn {
                    DataCell = new TextBoxCell("HasAnimationsAsString"),
                    HeaderText = "Animations",
                    Editable = false,
                    Sortable = true,
                    AutoSize = true,
                }
            },
        };

        Canvas = new Drawable() {
            BackgroundColor = Color.FromArgb(0x1c, 0x1e, 0x1f),
            CanFocus = true,
        };

        CheckBox checkBoxShowAllFiles = new() {
            Text = "Show All Files",
            Checked = ShowAllFiles,
        };
        checkBoxShowAllFiles.CheckedBinding.Bind(this, f => f.ShowAllFiles);

        CheckBox checkBoxDarkMode = new() {
            Text = "Dark Mode",
            Checked = true
        };
        checkBoxDarkMode.CheckedBinding.Bind(this, f => f.DarkMode);

        scrollableContainer = new() {
            Content = Canvas,
            ExpandContentWidth = true,
            ExpandContentHeight = true,
            Padding = new Padding(0),
        };

        this.Content = new Splitter {
            Orientation = Orientation.Horizontal,
            Panel1MinimumSize = 300,
            SplitterWidth = 10,
            Panel1 = new StackLayout {
                Orientation = Orientation.Vertical,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Spacing = 10,
                Padding = new Padding(0, 0, 10, 0),
                Items = {
                    new StackLayoutItem {
                        Control = TreeGridViewFolders,
                        Expand = true,
                    },
                    new StackLayout {
                        Orientation = Orientation.Horizontal,
                        Items = {
                            new StackLayoutItem(checkBoxShowAllFiles, true),
                            new StackLayoutItem(checkBoxDarkMode, true),
                        }
                    },
                },
            },
            Panel2 = scrollableContainer,
        };
    }
}