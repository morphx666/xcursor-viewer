using Eto.Drawing;
using System;

namespace xcursor_viewer;

// https://www.x.org/releases/X11R7.7/doc/man/man3/Xcursor.3.xhtml

internal class X11Structs {
    internal const UInt32 XCURSOR_MAGIC = 0x58637572;

    internal struct X11CursorHeader {
        public UInt32 Magic;
        public UInt32 Size;
        public UInt32 Version;
        public UInt32 TableOfContentsCount;
        public X11CursorTableOfContents[] TableOfContents;
    }

    internal struct X11CursorTableOfContents {
        public UInt32 Type;
        public UInt32 SubType;
        public UInt32 Position;
    }

    internal struct X11Chunk {
        public UInt32 Size;
        public UInt32 Type;
        public UInt32 SubType;
        public UInt32 Version;
    }

    internal struct X11Comment {
        public X11Chunk Chunk;
        public UInt32 Length;
        public string Comment; // UTF8 string
    }

    internal struct X11Image {
        public X11Chunk Chunk;
        public UInt32 Width;
        public UInt32 Height;
        public UInt32 XHot;
        public UInt32 YHot;
        public UInt32 Delay;
        public UInt32[] Pixels;
    }
}
