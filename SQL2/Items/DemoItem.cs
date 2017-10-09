﻿#region ================= Namespaces

using System.IO;
using System.Windows.Media;

#endregion

namespace mxd.SQL2.Items
{
	public class DemoItem : AbstractItem
	{
		#region ================= Default items

		public static readonly DemoItem None = new DemoItem(NAME_NONE);

		#endregion

		#region ================= Variables

		private readonly string modname; // Stored only on QWD demos, so can be empty...
		private readonly string mapfilepath;
		private readonly string maptitle;
		private readonly bool isinvalid;

		#endregion

		#region ================= Properties

		// Value: demos\somedemo.dem
		// Title: demos\somedemo.dem | map: Benis Devastation
		public string ModName => modname; // xatrix / id1 etc.
		public string MapFilePath => mapfilepath; // maps/somemap.bsp
		public string MapTitle => maptitle; // Benis Devastation
		public bool IsInvalid => isinvalid;

		public override ItemType Type => ItemType.DEMO;
		private new bool IsRandom; // No random demos

		#endregion

		#region ================= Constructors

		private DemoItem(string name) : base(name, "")
		{
			this.maptitle = name;
		}

		// "demos\dm3_demo.dem", "maps\dm3.bsp", "Whatever Title DM3 Has"
		public DemoItem(string filename, string mapfilepath, string maptitle) : base(filename + " | map: " + maptitle, filename)
		{
			this.modname = string.Empty;
			this.mapfilepath = mapfilepath;
			this.maptitle = maptitle;
			this.foreground = Brushes.Black;
		}

		// "qw", "demos\dm3_demo.dem", "maps\dm3.bsp", "Whatever Title DM3 Has"
		public DemoItem(string modname, string filename, string mapfilepath, string maptitle) : base(filename + " | map: " + maptitle, filename)
		{
			this.modname = modname;
			this.mapfilepath = mapfilepath;
			this.maptitle = maptitle;
			this.foreground = Brushes.Black;
		}

		public DemoItem(string filename, string message, bool isinvalid = true) : base((string.IsNullOrEmpty(message) ? filename : filename + " | " + message), filename)
		{
			this.isinvalid = isinvalid;
			this.maptitle = Path.GetFileName(filename);
			this.foreground = (isinvalid ? Brushes.DarkRed : Brushes.Black);
		}

		#endregion
	}
}