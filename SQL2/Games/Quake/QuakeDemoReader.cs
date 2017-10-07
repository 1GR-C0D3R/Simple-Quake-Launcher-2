﻿#region ================= Namespaces

using System;
using System.Collections.Generic;
using System.IO;
using mxd.SQL2.Data;
using mxd.SQL2.Items;
using mxd.SQL2.Tools;

#endregion

namespace mxd.SQL2.Games.Quake
{
	public static class QuakeDemoReader
	{
		#region ================= Constants

		// DEM protocols
		private const int PROTOCOL_NETQUAKE = 15;
		private const int PROTOCOL_FITZQUAKE = 666;
		private const int PROTOCOL_RMQ = 999;

		// QW protocols
		private static readonly HashSet<int> ProtocolsQW = new HashSet<int> { 24, 25, 26, 27, 28 }; // The not so many QW PROTOCOL_VERSIONs...

		private const int GAME_COOP = 0;
		private const int GAME_DEATHMATCH = 1;

		private const int BLOCK_CLIENT = 0;
		private const int BLOCK_SERVER = 1;
		private const int BLOCK_FRAME = 2;

		private const int SVC_STUFFTEXT = 9;
		private const int SVC_SERVERINFO = 11;
		private const int SVC_CDTRACK = 32;
		private const int SVC_MODELLIST = 45;
		private const int SVC_SOUNDLIST = 46;

		#endregion

		#region ================= GetDemoInfo

		public static DemoItem GetDemoInfo(string demoname, BinaryReader reader)
		{
			string ext = Path.GetExtension(demoname);
			if(string.IsNullOrEmpty(ext)) return null;

			switch(ext.ToUpperInvariant())
			{
				case ".DEM": return GetDEMInfo(demoname, reader);
				case ".MVD": return GetMVDInfo(demoname, reader);
				case ".QWD": return GetQWDInfo(demoname, reader);
				default: throw new NotImplementedException("Unsupported demo type: " + ext);
			}
		}

		// https://www.quakewiki.net/archives/demospecs/dem/dem.html
		private static DemoItem GetDEMInfo(string demoname, BinaryReader reader)
		{
			// CD track (string terminated by '\n' (0x0A in ASCII))

			// Block header:
			// Block size (int32)
			// Camera angles (int32 x 3)

			// SVC_SERVERINFO (byte, 0x0B)
			// protocol (int32)  -> 666, 999 or 15
			// protocolflags (int32) - only when protocol == 999
			// maxclients (byte) should be in 1 .. 16 range
			// gametype (byte) - 0 -> coop, 1 -> deathmatch
			// map title (null-terminated string)
			// map filename (null-terminated string) "maps/mymap.bsp"

			// CD-track: skip a decimal integer possibly with a leading '-', followed by a '\n'...
			if(!reader.SkipString(13, '\n')) return null;

			// Read block header...
			int blocklength = reader.ReadInt32();
			if(reader.BaseStream.Position + blocklength >= reader.BaseStream.Length) return null;
			reader.BaseStream.Position += 12; // Skip camera angles

			// Next should be SVC_SERVERINFO
			byte messagetype = reader.ReadByte();
			if(messagetype != SVC_SERVERINFO) return null;

			int protocol = reader.ReadInt32();
			if(protocol != PROTOCOL_NETQUAKE && protocol != PROTOCOL_FITZQUAKE && protocol != PROTOCOL_RMQ) return null;
			if(protocol == PROTOCOL_RMQ) reader.BaseStream.Position += 4; // Skip RMQ protocolflags (int32)

			int maxclients = reader.ReadByte();
			if(maxclients < 1 || maxclients > 16) return null;

			int gametype = reader.ReadByte();
			if(gametype != GAME_COOP && gametype != GAME_DEATHMATCH) return null;

			string maptitle = reader.ReadMapTitle(blocklength, QuakeFont.CharMap); // Map title can contain bogus chars...
			string mapfilepath = reader.ReadString(blocklength);
			if(string.IsNullOrEmpty(mapfilepath)) return null;
			if(string.IsNullOrEmpty(maptitle)) maptitle = Path.GetFileName(mapfilepath);

			// Done
			return new DemoItem(demoname, mapfilepath, maptitle);
		}

		// https://www.quakewiki.net/archives/demospecs/qwd/qwd.html
		private static DemoItem GetQWDInfo(string demoname, BinaryReader reader)
		{
			// Block header:
			// float time;                 
			// char code; // QWDBlockType.SERVER for server block

			// Server block:
			// long blocksize;
			// unsigned long seq_rel_1; // (!= 0xFFFFFFFF) for Game block

			// Game block:
			// unsigned long seq_rel_2;
			// char messages[blocksize - 8];

			string game = string.Empty;
			string maptitle = string.Empty;
			string mapfilepath = string.Empty;
			int protocol = 0;
			bool alldatafound = false;

			// Read blocks...
			while(reader.BaseStream.Position < reader.BaseStream.Length)
			{
				if(alldatafound) break;

				// Read block header...
				reader.BaseStream.Position += 4; // Skip time
				int code = reader.ReadByte();
				if(code != BLOCK_SERVER) return null;

				// Read as Server block...
				int blocklength = reader.ReadInt32();
				long blockend = reader.BaseStream.Position + blocklength;
				if(blockend >= reader.BaseStream.Length) return null;
				uint serverblocktype = reader.ReadUInt32(); // 13 // connectionless block (== 0xFFFFFFFF) or a game block (!= 0xFFFFFFFF).
				if(serverblocktype == uint.MaxValue) return null;

				// Read as Game block...
				reader.BaseStream.Position += 4; // Skip seq_rel_2

				while(reader.BaseStream.Position < blockend)
				{
					if(alldatafound) break;

					// Read messages...
					int message = reader.ReadByte();
					switch(message)
					{
						// SVC_SERVERINFO
						// long serverversion; // the protocol version coming from the server.
						// long age; // the number of levels analysed since the existence of the server process. Starts with 1.
						// char* game; // the QuakeWorld game directory. It has usually the value "qw";
						// byte client; // the client id.
						// char* mapname; // the name of the level.
						// 10 unrelated floats

						case SVC_SERVERINFO:
							protocol = reader.ReadInt32();
							if(!ProtocolsQW.Contains(protocol)) return null;
							reader.BaseStream.Position += 4; // Skip age
							game = reader.ReadString('\0');
							reader.BaseStream.Position += 1; // Skip client
							maptitle = reader.ReadMapTitle(blocklength, QuakeFont.CharMap); // Map title can contain bogus chars...
							if(protocol > 24) reader.BaseStream.Position += 40; // Skip 10 floats...
							break;

						case SVC_CDTRACK:
							reader.BaseStream.Position += 1; // Skip CD track number
							break;

						case SVC_STUFFTEXT:
							reader.SkipString(2048);
							break;

						case SVC_MODELLIST: // First model should be the map name
							if(protocol > 25) reader.BaseStream.Position += 1; // Skip first model index...
							for(int i = 0; i < 256; i++)
							{
								string mdlname = reader.ReadString('\0');
								if(string.IsNullOrEmpty(mdlname)) break;
								if(mdlname.EndsWith(".bsp", StringComparison.OrdinalIgnoreCase))
								{
									mapfilepath = mdlname;
									alldatafound = true;
									break;
								}
							}
							if(protocol > 25) reader.BaseStream.Position += 1; // Skip next model index...
							break;

						case SVC_SOUNDLIST:
							if(protocol > 25) reader.BaseStream.Position += 1; // Skip first sound index...
							for(int i = 0; i < 256; i++)
							{
								string sndname = reader.ReadString('\0');
								if(string.IsNullOrEmpty(sndname)) break;
							}
							if(protocol > 25) reader.BaseStream.Position += 1; // Skip next sound index...
							break;

						default:
							return null;
					}
				}
			}

			// Done
			return (alldatafound ? new DemoItem(game, demoname, mapfilepath, maptitle) : null);
		}

		//TODO: Hacked in, needs more testing or proper format spec...
		private static DemoItem GetMVDInfo(string demoname, BinaryReader reader)
		{
			string game = string.Empty;
			string maptitle = string.Empty;
			string mapfilepath = string.Empty;
			int protocol = 0;
			bool alldatafound = false;

			// Read blocks...
			while(reader.BaseStream.Position < reader.BaseStream.Length)
			{
				if(alldatafound) break;

				// Read block header...
				reader.BaseStream.Position += 2; // Skip ??? (0x00 0x01 or 0x00 0x06)
				int blocklength = reader.ReadInt32();
				long blockend = reader.BaseStream.Position + blocklength;
				if(blockend >= reader.BaseStream.Length) return null;

				while(reader.BaseStream.Position < blockend)
				{
					if(alldatafound) break;

					// Read messages...
					int message = reader.ReadByte();
					switch(message)
					{
						// SVC_SERVERINFO
						// long serverversion; // the protocol version coming from the server.
						// long age; // the number of levels analysed since the existence of the server process. Starts with 1.
						// char* game; // the QuakeWorld game directory. It has usually the value "qw";
						// long client; // the client id.
						// char* mapname; // the name of the level.
						// 10 unrelated floats

						case SVC_SERVERINFO:
							protocol = reader.ReadInt32();
							if(!ProtocolsQW.Contains(protocol)) return null;
							reader.BaseStream.Position += 4; // Skip age
							game = reader.ReadString('\0');
							reader.BaseStream.Position += 4; // Skip ???
							maptitle = reader.ReadMapTitle(blocklength, QuakeFont.CharMap); // Map title can contain bogus chars...
							if(protocol > 24) reader.BaseStream.Position += 40; // Skip 10 floats...
							break;

						case SVC_CDTRACK:
							reader.BaseStream.Position += 1; // Skip CD track number
							break;

						case SVC_STUFFTEXT:
							reader.SkipString(2048);
							break;

						case SVC_MODELLIST: // First model should be the map name
							if(protocol > 25) reader.BaseStream.Position += 1; // Skip first model index...
							for(int i = 0; i < 256; i++)
							{
								string mdlname = reader.ReadString('\0');
								if(string.IsNullOrEmpty(mdlname)) break;
								if(mdlname.EndsWith(".bsp", StringComparison.OrdinalIgnoreCase))
								{
									mapfilepath = mdlname;
									alldatafound = true;
									break;
								}
							}
							if(protocol > 25) reader.BaseStream.Position += 1; // Skip next model index...
							break;

						case SVC_SOUNDLIST:
							if(protocol > 25) reader.BaseStream.Position += 1; // Skip first sound index...
							for(int i = 0; i < 256; i++)
							{
								string sndname = reader.ReadString('\0');
								if(string.IsNullOrEmpty(sndname)) break;
							}
							if(protocol > 25) reader.BaseStream.Position += 1; // Skip next sound index...
							break;

						default:
							return null;
					}
				}
			}

			// Done
			return (alldatafound ? new DemoItem(game, demoname, mapfilepath, maptitle) : null);
		}

		#endregion
	}
}
