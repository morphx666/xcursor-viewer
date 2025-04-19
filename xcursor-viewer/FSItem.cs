using Eto.Forms;
using Eto.Drawing;
using System.IO;
using System.Linq;

namespace xcursor_viewer;

public partial class MainForm {
    private class FSItem : TreeGridItem {
        public string Name { get; set; }
        public string Path { get; }
        public XCursor Cursor { get; }
        public Icon Icon { get; }
        public string ImagesCountAsString  => Cursor?.Images.Count.ToString() ?? "";
        public string HasAnimationsAsString => (Cursor?.Images.Any(f => f.Count > 1) ?? false) ? "✓" : "";
        public bool IsFile { get; }

        public FSItem() { }

        public FSItem(string name, string path, bool isFile, Bitmap icon = null) {
            Name = name;
            Path = path;
            IsFile = isFile;

            if(isFile && File.Exists(path) && XCursor.IsXCursor(path)) {
                Cursor = new XCursor(path);
                Icon = Cursor.Images.First().First().WithSize(22, 22);
            } else {
                Cursor = null;
                Icon = icon.WithSize(22, 22);

                if(!IsEmpty(path)) base.Children.Add(new FSItem());
            }
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
}