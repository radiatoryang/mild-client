using UnityEngine;
using UnityEngine.UI;
//using Unity.IO.Compression;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;


public class Mild : MonoBehaviour {

	public static Mild instance;

	[SerializeField, Tooltip("must start with ws:// and end with a port, e.g. ws://myserver.com:8000")] 
	string serverAddress = "ws://nodejs-radiatorgame.rhcloud.com:8000";
	[SerializeField] string serverUsername = "serverUsername";
	[SerializeField] string serverPassword = "serverSecretPassword";
	static WebSocket webSocket;

	[Tooltip("how many times, per second, to broadcast SyncMyPlayer or SyncOtherPlayer messages at player objects... set to -1 to turn it off")]
	public int tickRate = 20;

	// TODO: remove, replace with OnGUI
	[SerializeField] Text textOutput;
	[SerializeField] InputField consoleInput;


	[SerializeField] Transform playerPrefab;
	static Dictionary<int, Transform> idToPlayer = new Dictionary<int, Transform>();
	static List<Transform> allPlayers { get { return idToPlayer.Values.ToList(); } }
	public static int myID = -1;
	public static string myName = "NEW_PLAYER";
	public static Transform myPlayer { get { return GetPlayer(myID); } }

	public static Transform GetPlayer (int id) {
		if ( idToPlayer != null && idToPlayer.ContainsKey(id) ) {
			return Mild.idToPlayer[id];
		} else {
			return null;
		}
	}
	public static bool IsMyTransform( Transform transform ) {
		return transform == myPlayer || transform.parent == myPlayer;
	}
		
	void Awake () {
		instance = this;
		idToPlayer.Clear();
		myID = -1;
	}

	// Use this for initialization
	void Start () {
		StartCoroutine( ServerCoroutine() );
		StartCoroutine( SyncCoroutine() );
	}

	void Update () {
		foreach ( var kvp in idToPlayer ) {
			if ( kvp.Key == myID ) {
				kvp.Value.BroadcastMessage( "UpdateMyPlayer", SendMessageOptions.DontRequireReceiver );
			} else {
				kvp.Value.BroadcastMessage( "UpdateOtherPlayer", SendMessageOptions.DontRequireReceiver );
			}
		}
	}

	IEnumerator SyncCoroutine() {
		while (tickRate > 0) {
			foreach ( var kvp in idToPlayer ) {
				if ( kvp.Key == myID ) {
					kvp.Value.BroadcastMessage( "TickMyPlayer", SendMessageOptions.DontRequireReceiver );
				} else {
					kvp.Value.BroadcastMessage( "TickOtherPlayer", SendMessageOptions.DontRequireReceiver );
				}
			}
			yield return new WaitForSeconds( 1f / tickRate );
		}
	}


	public static void Cmd( string functionName, int playerID=-1 ) {
		SendCommand("cmd", functionName, "", playerID );
	}

	public static void CmdBool( string functionName, bool boolParam, int playerID=-1 ) {
		SendCommand("cmdBool", functionName, boolParam ? "1" : "0", playerID );
	}

	public static void CmdInt( string functionName, int intParam, int playerID=-1 ) {
		SendCommand("cmdInt", functionName, intParam.ToString(), playerID );
	}

	public static void CmdFloat( string functionName, float floatParam, int playerID=-1 ) {
		SendCommand("cmdFloat", functionName, floatParam.ToString("F2"), playerID );
	}
		
	public static void CmdVec2( string functionName, Vector2 vectorParam, int playerID=-1 ) {
		SendCommand( "cmdVec2", functionName, string.Format("{0:F2},{1:F2}", vectorParam.x, vectorParam.y), playerID );
	}

	public static void CmdVec3( string functionName, Vector3 vectorParam, int playerID=-1 ) {
		// TODO: send bytes instead of float strings
		SendCommand( "cmdVec3", functionName, string.Format("{0:F2},{1:F2},{2:F2}", vectorParam.x, vectorParam.y, vectorParam.z), playerID );
	}

	public static void CmdQuat ( string functionName, Quaternion quatParam, int playerID=-1) {
		// TODO: send bytes instead of float strings
		SendCommand( "cmdQuat", functionName, string.Format("{0:F2},{1:F2},{2:F2},{3:F2}", quatParam.x, quatParam.y, quatParam.z, quatParam.w), playerID );
	}

	public static void CmdString( string functionName, string stringParam, int playerID=-1 ) {
		SendCommand( "cmdStr", functionName, stringParam, playerID );
	}

	/// <summary>
	/// You probably want one of the "Cmd___" functions instead
	/// </summary>
	public static void SendCommand (string title, string functionName, string param, int playerID=-1 ) {
		if ( playerID < 0 ) { playerID = myID; }

		SendToServer( string.Format( "{0}{4}{1}`{2}`{3}", title, functionName, param, playerID, msgSeparator) );
	}


	// "join" message from server, when a player joins (including when you join a server)
	void OnPlayerJoin( int newID ) {
		InstantiatePlayer(newID);
		Log( "client#" + newID.ToString() + " joined the server" );
	}

	// "leave" message from server, when a player leaves (including when you leave)
	void OnPlayerLeave( int oldID ) {
		DestroyPlayer( oldID );
		Log( "client#" + oldID.ToString() + " left the server" );
	}

	// "welcome" message from server, when we just joined
	void OnPlayerWelcome( int[] allPlayerIDs ) {
		Debug.Log(allPlayerIDs.Length);
		for( int i=0; i<allPlayerIDs.Length; i++) {
			InstantiatePlayer( allPlayerIDs[i] );
		}

		// as new player, you are the last ID
		myID = allPlayerIDs[ allPlayerIDs.Length-1 ];
		Log("welcome! you are client#" + myID.ToString() );
	}

	void OnPlayerChat ( string speakerName, string text) {
		Log( string.Format("[{0}] > \"{1}\"", speakerName, text) );
	}

	// generic, when we get any server message
	const char msgSeparator = '^';
	void OnServerMessage( byte[] serverMessage ) {
		string message = Unzip( serverMessage );
		string[] parts = message.Split( msgSeparator);
		string title = parts[0];
		string body = parts.Length > 1 ? parts[1] : "";
		//Log(message);

		switch ( title ) {
		case "newjoin":
			OnPlayerJoin( int.Parse(body) );
			foreach ( var player in allPlayers ) { // call OnPlayerJoin on all player objects
				player.SendMessage("OnPlayerJoin", int.Parse(body), SendMessageOptions.DontRequireReceiver );
			}
			break;
		case "leave":
			OnPlayerLeave( int.Parse(body) );
			foreach ( var player in allPlayers ) { // call 
				player.SendMessage("OnPlayerLeave", int.Parse(body), SendMessageOptions.DontRequireReceiver );
			}
			break;
		case "welcome":
			// parse body text into an array of player ID #s
			string[] allPlayerIDstrings = body.Split(','); 
			int[] allPlayerIDs = new int[ allPlayerIDstrings.Length-1 ]; // HACK: the last array item will always be empty
			for ( int i=0; i<allPlayerIDs.Length; i++) {
				allPlayerIDs[i] = int.Parse( allPlayerIDstrings[i] );
			}
			OnPlayerWelcome( allPlayerIDs );
			break;
		case "chat":
			string[] chatParts = body.Split('`');
			OnPlayerChat(chatParts[0], chatParts[1]);
			break;
		case "cmd":
			string[] cmdParts = body.Split('`');
			GetPlayer( int.Parse(cmdParts[2]) ).SendMessage(cmdParts[0], SendMessageOptions.DontRequireReceiver);
			break;
		case "cmdBool":
			string[] cmdBoolParts = body.Split('`');
			GetPlayer( int.Parse(cmdBoolParts[2]) ).SendMessage(cmdBoolParts[0], int.Parse(cmdBoolParts[1]) == 1 ? true : false, SendMessageOptions.DontRequireReceiver);
			break;
		case "cmdFloat":
			string[] cmdFloatParts = body.Split('`');
			GetPlayer( int.Parse(cmdFloatParts[2]) ).SendMessage(cmdFloatParts[0], float.Parse(cmdFloatParts[1]), SendMessageOptions.DontRequireReceiver);
			break;
		case "cmdInt":
			string[] cmdIntParts = body.Split('`');
			GetPlayer( int.Parse(cmdIntParts[2]) ).SendMessage(cmdIntParts[0], int.Parse(cmdIntParts[1]), SendMessageOptions.DontRequireReceiver);
			break;
		case "cmdStr":
			string[] cmdStringParts = body.Split('`');
			GetPlayer( int.Parse(cmdStringParts[2]) ).SendMessage(cmdStringParts[0], cmdStringParts[1], SendMessageOptions.DontRequireReceiver);
			break;
		case "cmdVec2":
			string[] cmdVec2Parts = body.Split('`');
			string[] vec2Parts = cmdVec2Parts[1].Split(',');
			Vector2 vec2 = new Vector2( float.Parse(vec2Parts[0]), float.Parse(vec2Parts[1]) );
			GetPlayer( int.Parse(cmdVec2Parts[2]) ).SendMessage(cmdVec2Parts[0], vec2, SendMessageOptions.DontRequireReceiver);
			break;
		case "cmdVec3":
			string[] cmdVec3Parts = body.Split('`');
			string[] vec3Parts = cmdVec3Parts[1].Split(',');
			Vector3 vec3 = new Vector3( float.Parse(vec3Parts[0]), float.Parse(vec3Parts[1]), float.Parse(vec3Parts[2])  );
			GetPlayer( int.Parse(cmdVec3Parts[2]) ).SendMessage(cmdVec3Parts[0], vec3, SendMessageOptions.DontRequireReceiver);
			break;
		case "cmdQuat":
			string[] cmdQuatParts = body.Split('`');
			string[] quatParts = cmdQuatParts[1].Split(',');
			Quaternion quat = new Quaternion( float.Parse(quatParts[0]), float.Parse(quatParts[1]), float.Parse(quatParts[2]), float.Parse(quatParts[3])  );
			GetPlayer( int.Parse(cmdQuatParts[2]) ).SendMessage(cmdQuatParts[0], quat, SendMessageOptions.DontRequireReceiver);
			break;
		default:
			Log("unknown message from server: " + message);
			break;
		}
	}
		
	void InstantiatePlayer (int newID) {
		var newPlayer = (Transform)Instantiate(playerPrefab);

		if ( idToPlayer.ContainsKey(newID) ) { // cleanup, just in case
			OnPlayerLeave(newID);
		}

		idToPlayer.Add( newID, newPlayer);
	}

	void DestroyPlayer (int oldID) {
		if ( idToPlayer.ContainsKey(oldID) ) {
			Destroy( idToPlayer[oldID].gameObject );
			idToPlayer.Remove( oldID );
		}
	}




	static void SendToServer (string stringData ) {
		//webSocket.SendString( stringData );
		webSocket.Send( Zip( stringData ) );
	}

	public void SendChat () {
		SendToServer( "chat^" + "client" + myID.ToString() + "`" + consoleInput.text );
		consoleInput.text = "";
	}

	IEnumerator ServerCoroutine () {
		yield return new WaitForSeconds(1f);
		webSocket = new WebSocket(new Uri(serverAddress) );
		yield return StartCoroutine(webSocket.Connect( serverUsername, serverPassword ));
		yield return 0;
		Log("(connection established)");

		int i=0;
		while (true) {
			var reply = webSocket.Recv();
			if (reply != null) {
				OnServerMessage( reply );
			}
			if (webSocket.error != null) {
				Log ("Error: "+webSocket.error);
				break;
			}
			yield return 0;
		}
		webSocket.Close ();
	}

	void Log( string logMessage ) {
		Debug.Log( logMessage );
		textOutput.text += "\n" + logMessage;
	}

	void OnApplicationQuit () {
		if ( webSocket != null ) {
			webSocket.Close();
		}
	}


	// compression is actually pretty unnecessary, given how small these packets are?
	// if I ever implement nagle / pack delays, then maybe it would make sense

	// from http://stackoverflow.com/questions/19364497/how-to-tell-if-a-byte-array-is-gzipped
	public static bool IsGZip(byte[] arr) {
		return arr.Length >= 2 && arr[0] == 31 && arr[1] == 139;
	}

	// from http://stackoverflow.com/questions/7343465/compression-decompression-string-with-c-sharp
//	public static void CopyTo(Stream src, Stream dest) {
//		byte[] bytes = new byte[4096];
//
//		int cnt;
//
//		while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0) {
//			dest.Write(bytes, 0, cnt);
//		}
//	}

	public static byte[] Zip(string str) {
		var bytes = Encoding.UTF8.GetBytes(str);

//		using (var msi = new MemoryStream(bytes))
//		using (var mso = new MemoryStream()) {
//			using (var gs = new GZipStream(mso, CompressionMode.Compress)) {
//				//msi.CopyTo(gs);
//				CopyTo(msi, gs);
//			}
//
//			return mso.ToArray();
//		}

		return bytes;
	}

	public static string Unzip(byte[] bytes) {
//		using (var msi = new MemoryStream(bytes))
//		using (var mso = new MemoryStream()) {
//			using (var gs = new GZipStream(msi, CompressionMode.Decompress)) {
//				//gs.CopyTo(mso);
//				CopyTo(gs, mso);
//			}
//
//			return Encoding.UTF8.GetString(mso.ToArray());
//		}
		return Encoding.UTF8.GetString(bytes);
	}
		
}

/*

// from http://deciduousgames.blogspot.com/2013/04/unity-serializer.html
// Author: Richard Pieterse, April 2013 
public class UnitySerializer {  

	private List<byte> byteStream = new List<byte>();  
	private byte[] byteArray;  
	private int index = 0;  

	/// <summary>  
	/// Returns the stream as a Byte Array  
	/// </summary>  
	public byte[] ByteArray  
	{  
		get  
		{  
			if ( byteArray == null || byteStream.Count != byteArray.Length)  
				byteArray = byteStream.ToArray();  

			return byteArray;  
		}  
	}  

	/// <summary>  
	/// Create a new empty stream  
	/// </summary>  
	public UnitySerializer()  
	{  

	}  

	/// <summary>  
	/// Initialiaze a stream from a byte array.  
	/// Used for deserilaizing a byte array  
	/// </summary>  
	/// <param name="ByteArray"></param>  
	public UnitySerializer(byte[] ByteArray)  
	{  
		byteArray = ByteArray;  
		byteStream = new List<byte>(ByteArray);  
	}  



	// --- double ---  
	public void Serialize(double d)  
	{  
		byteStream.AddRange( BitConverter.GetBytes(d));  

	}  

	public double DeserializeDouble()  
	{  
		double d = BitConverter.ToDouble(ByteArray, index); index += 8;  
		return d;  
	}  
	//  

	// --- bool ---  
	public void Serialize(bool b)  
	{  
		byteStream.AddRange(BitConverter.GetBytes(b));  
	}  

	public bool DeserializeBool()  
	{  
		bool b = BitConverter.ToBoolean(ByteArray, index); index += 1;  
		return b;  
	}  
	//  

	// --- Vector2 ---  
	public void Serialize(Vector2 v)  
	{  
		byteStream.AddRange(GetBytes(v));  
	}  

	public Vector2 DeserializeVector2()  
	{  
		Vector2 vector2 = new Vector2();  
		vector2.x = BitConverter.ToSingle(ByteArray, index); index += 4;  
		vector2.y = BitConverter.ToSingle(ByteArray, index); index += 4;  
		return vector2;  
	}  
	//  

	// --- Vector3 ---  
	public void Serialize(Vector3 v)  
	{  
		byteStream.AddRange(GetBytes(v));  
	}  

	public Vector3 DeserializeVector3()  
	{  
		Vector3 vector3 = new Vector3();  
		vector3.x = BitConverter.ToSingle(ByteArray, index); index += 4;  
		vector3.y = BitConverter.ToSingle(ByteArray, index); index += 4;  
		vector3.z = BitConverter.ToSingle(ByteArray, index); index += 4;  
		return vector3;  
	}  
	//  

	// --- Type ---  
	public void Serialize(System.Type t)  
	{  
		// serialize type as string  
		string typeStr = t.ToString();  
		Serialize(typeStr);  
	}  

	public Type DeserializeType()  
	{  
		// type stored as string  
		string typeStr = DeserializeString();  
		return Type.GetType(typeStr); ;  
	}  
	//  

	// --- String ---  
	public void Serialize(string s)  
	{  
		// add the length as a header  
		byteStream.AddRange(BitConverter.GetBytes(s.Length));  
		foreach (char c in s)  
			byteStream.Add((byte)c);  
	}  

	public string DeserializeString()  
	{  
		int length = BitConverter.ToInt32(ByteArray, index); index += 4;  
		string s = "";  
		for (int i = 0; i < length; i++)  
		{  
			s += (char)ByteArray[index];  
			index++;  
		}  

		return s;  
	}  
	//  

	// --- byte[] ---  
	public void Serialize(byte[] b)  
	{  
		// add the length as a header  
		byteStream.AddRange(BitConverter.GetBytes(b.Length));  
		byteStream.AddRange(b);  
	}  

	public byte[] DeserializeByteArray()  
	{  
		int length = BitConverter.ToInt32(ByteArray, index); index += 4;  
		byte[] bytes = new byte[length];  
		for (int i = 0; i < length; i++)  
		{  
			bytes[i] = ByteArray[index];  
			index++;  
		}  

		return bytes;  
	}  
	//  

	// --- Quaternion ---  
	public void Serialize(Quaternion q)  
	{  
		byteStream.AddRange(GetBytes(q));  
	}  

	public Quaternion DeserializeQuaternion()  
	{  
		Quaternion quat = new Quaternion();  
		quat.x = BitConverter.ToSingle(ByteArray, index); index += 4;  
		quat.y = BitConverter.ToSingle(ByteArray, index); index += 4;  
		quat.z = BitConverter.ToSingle(ByteArray, index); index += 4;  
		quat.w = BitConverter.ToSingle(ByteArray, index); index += 4;  
		return quat;  
	}  
	//  

	// --- float ---  
	public void Serialize(float f)  
	{  
		byteStream.AddRange(BitConverter.GetBytes(f));  
	}  

	public float DeserializeFloat()  
	{  
		float f = BitConverter.ToSingle(ByteArray, index); index += 4;  
		return f;  
	}  
	//  

	// --- int ---  
	public void Serialize(int i)  
	{  
		byteStream.AddRange(BitConverter.GetBytes(i));  
	}  

	public int DeserializeInt()  
	{  
		int i = BitConverter.ToInt32(ByteArray, index); index += 4;  
		return i;  
	}  
	//  

	// --- internal ----  
	Vector3 DeserializeVector3(byte[] bytes, ref int index)  
	{  
		Vector3 vector3 = new Vector3();  
		vector3.x = BitConverter.ToSingle(bytes, index); index += 4;  
		vector3.y = BitConverter.ToSingle(bytes, index); index += 4;  
		vector3.z = BitConverter.ToSingle(bytes, index); index += 4;  

		return vector3;  
	}  

	Quaternion DeserializeQuaternion(byte[] bytes, ref int index)  
	{  
		Quaternion quat = new Quaternion();  
		quat.x = BitConverter.ToSingle(bytes, index); index += 4;  
		quat.y = BitConverter.ToSingle(bytes, index); index += 4;  
		quat.z = BitConverter.ToSingle(bytes, index); index += 4;  
		quat.w = BitConverter.ToSingle(bytes, index); index += 4;  
		return quat;  
	}  

	byte[] GetBytes(Vector2 v)  
	{  
		List<byte> bytes = new List<byte>(8);  
		bytes.AddRange(BitConverter.GetBytes(v.x));  
		bytes.AddRange(BitConverter.GetBytes(v.y));  
		return bytes.ToArray();  
	}  

	byte[] GetBytes(Vector3 v)  
	{  
		List<byte> bytes = new List<byte>(12);  
		bytes.AddRange(BitConverter.GetBytes(v.x));  
		bytes.AddRange(BitConverter.GetBytes(v.y));  
		bytes.AddRange(BitConverter.GetBytes(v.z));  
		return bytes.ToArray();  
	}  

	byte[] GetBytes(Quaternion q)  
	{  
		List<byte> bytes = new List<byte>(16);  
		bytes.AddRange(BitConverter.GetBytes(q.x));  
		bytes.AddRange(BitConverter.GetBytes(q.y));  
		bytes.AddRange(BitConverter.GetBytes(q.z));  
		bytes.AddRange(BitConverter.GetBytes(q.w));  
		return bytes.ToArray();  
	}  

	public static void Example()  
	{  
		//  
		Debug.Log("--- UnitySerializer Example ---");  
		Vector2 point      = UnityEngine.Random.insideUnitCircle;  
		Vector3 position    = UnityEngine.Random.onUnitSphere;  
		Quaternion quaternion  = UnityEngine.Random.rotation;  
		float f         = UnityEngine.Random.value;  
		int i          = UnityEngine.Random.Range(0, 10000);  
		double d        = (double)UnityEngine.Random.Range(0, 10000);  
		string s        = "Brundle Fly";  
		bool b         = UnityEngine.Random.value < 0.5f ? true : false;  
		System.Type type    = typeof(UnitySerializer);  

		//  
		Debug.Log("--- Before ---");  
		Debug.Log(point + " " + position + " " + quaternion + " " + f + " " + d + " " + s + " " + b + " " + type);  

		//  
		Debug.Log("--- Serialize ---");  
		UnitySerializer us = new UnitySerializer();  
		us.Serialize(point);  
		us.Serialize(position);  
		us.Serialize(quaternion);  
		us.Serialize(f);  
		us.Serialize(i);  
		us.Serialize(d);  
		us.Serialize(s);  
		us.Serialize(b);  
		us.Serialize(type);  
		byte[] byteArray = us.ByteArray;  

		// the array must be deserialized in the same order as it was serialized  
		Debug.Log("--- Deserialize ---");  
		UnitySerializer uds   = new UnitySerializer(byteArray);  
		Vector2 point2     = uds.DeserializeVector2();  
		Vector3 position2    = uds.DeserializeVector3();  
		Quaternion quaternion2 = uds.DeserializeQuaternion();  
		float f2        = uds.DeserializeFloat();  
		int i2         = uds.DeserializeInt();  
		double d2        = uds.DeserializeDouble();  
		string s2        = uds.DeserializeString();  
		bool b2         = uds.DeserializeBool();  
		System.Type type2    = uds.DeserializeType();  

		//  
		Debug.Log("--- After ---");  
		Debug.Log(point2 + " " + position2 + " " + quaternion2 + " " + f2 + " " + d2 + " " + s2 + " " + b2 + " " + type2);  
	}  

}  
*/