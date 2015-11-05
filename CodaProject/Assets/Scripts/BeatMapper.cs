using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Coda {

	public class BeatMapper {

		public static string filePath = "Assets/Coda/Beatmaps";

	}

	[System.Serializable]
	public struct Beat {
	    [XmlAttribute("time")]
	    public double timeStamp;
	    [XmlAttribute("freq")]
	    public float frequency;
	    [XmlAttribute("energy")]
	    public double energy;
	}

	[System.Serializable]
	[XmlRoot("BeatMap")]
	public class BeatMap {
	    [XmlArray("Beats")]
	    [XmlArrayItem("Beat")]
	    public List<Beat> beats;
	    public string fileName;
	    public float songLength;

	    public BeatMap() { }

	    public BeatMap(string name, float length) {
	        beats = new List<Beat>();
			fileName = name;
	        songLength = length;
	    }

	    public void AddBeat(double timeStamp, float frequency, double energy) {
	        Beat b;
	        b.timeStamp = timeStamp;
	        b.frequency = frequency;
	        b.energy = energy;
	        beats.Add(b);
	    }
	}

	public static class BeatMapWriter {

	    public static void WriteBeatMap(BeatMap map) {
	        // Check if beatmap folder exists
	        if (!Directory.Exists(BeatMapper.filePath)) {
	            Directory.CreateDirectory(BeatMapper.filePath);
	        }

	        XmlSerializer serializer = new XmlSerializer(typeof(BeatMap));
	        FileStream stream = new FileStream(BeatMapper.filePath + "/BeatMap_" + map.fileName + ".xml", FileMode.Create);
	        serializer.Serialize(stream, map);
	        stream.Close();
	        Debug.Log("Wrote beatmap to file");   
	    }
	}

	public static class BeatMapReader {

		public static BeatMap ReadBeatMap(TextAsset xmlFile) {
			XmlSerializer serializer = new XmlSerializer(typeof(BeatMap));
			FileStream stream = new FileStream(BeatMapper.filePath + "/" + xmlFile.name + ".xml", FileMode.Open);
			BeatMap newMap = (BeatMap)serializer.Deserialize(stream);
			stream.Close();
			return newMap;
		}

		public static BeatMap ReadBeatMap(string filePath) {
			XmlSerializer serializer = new XmlSerializer(typeof(BeatMap));
			FileStream stream = new FileStream(filePath, FileMode.Open);
			BeatMap newMap = (BeatMap)serializer.Deserialize(stream);
			stream.Close();
			return newMap;
		}

	}

}
