using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using UnityEngine;

public class WebSocketWrapper {
	private Uri mUrl;
	public WebSocketWrapper(string url) {
		mUrl = new Uri(url);
		string protocol = mUrl.Scheme;
		if (!protocol.Equals("ws") && !protocol.Equals("wss"))
			throw new ArgumentException("Unsupported protocol: " + protocol);
	}

	public WebSocketWrapper(Uri url) {
		mUrl = url;
		string protocol = mUrl.Scheme;
		if (!protocol.Equals("ws") && !protocol.Equals("wss"))
			throw new ArgumentException("Unsupported protocol: " + protocol);
	}

	public void SendString(string str) {
		Send(Encoding.UTF8.GetBytes (str));
	}

	public string RecvString() {
		byte[] retval = Recv();
		if (retval == null)
			return null;
		return Encoding.UTF8.GetString (retval,0,retval.Length);
	}


#if UNITY_WEBGL && !UNITY_EDITOR
	[DllImport("__Internal")]
	private static extern int SocketCreate (IntPtr url);

	[DllImport("__Internal")]
	private static extern int SocketState (int socketInstance);

	[DllImport("__Internal")]
	private static extern void SocketSend (int socketInstance, IntPtr ptr, int length);

	[DllImport("__Internal")]
	private static extern void SocketRecv (int socketInstance, IntPtr ptr, int length);

	[DllImport("__Internal")]
	private static extern int SocketRecvLength (int socketInstance);

	[DllImport("__Internal")]
	private static extern void SocketClose (int socketInstance);

	[DllImport("__Internal")]
	private static extern int SocketError (int socketInstance, IntPtr ptr, int length);

	int m_NativeRef = 0;

	public void Send(byte[] buffer) {
		IntPtr unmanagedPointer = Marshal.AllocHGlobal(buffer.Length);
		Marshal.Copy (buffer, 0, unmanagedPointer, buffer.Length);
		SocketSend (m_NativeRef, unmanagedPointer, buffer.Length);
		Marshal.FreeHGlobal(unmanagedPointer);
	}

	public byte[] Recv() {
		int length = SocketRecvLength (m_NativeRef);
		if (length == 0)
			return null;
		byte[] buffer = new byte[length];
		IntPtr unmanagedPointer = Marshal.AllocHGlobal(length);
		SocketRecv (m_NativeRef, unmanagedPointer, length);
		Marshal.Copy (unmanagedPointer, buffer, 0, length);
		Marshal.FreeHGlobal(unmanagedPointer);
		return buffer;
	}

	public IEnumerator Connect() {
		IntPtr stringPtr = Marshal.StringToHGlobalAnsi(mUrl.ToString());
		m_NativeRef = SocketCreate (stringPtr);
		Marshal.FreeHGlobal(stringPtr);
		while (SocketState(m_NativeRef) == 0)
			yield return 0;
	}
 
	public void Close() {
		SocketClose(m_NativeRef);
	}

	public string Error	{
		get {
			const int bufsize = 1024;
			IntPtr unmanagedPointer = Marshal.AllocHGlobal(bufsize);
			int result = SocketError (m_NativeRef, unmanagedPointer, bufsize);
			byte[] buffer = new byte[bufsize];
			Marshal.Copy (unmanagedPointer, buffer, 0, bufsize);
			Marshal.FreeHGlobal(unmanagedPointer);

			if (result == 0)
				return null;

			return Encoding.UTF8.GetString (buffer);				
		}
	}
#else
	WebSocketSharp.WebSocket m_Socket;
	Queue<byte[]> m_Messages = new Queue<byte[]>();
	bool m_IsConnected = false;
	string m_Error = null;
	public IEnumerator Connect() {
		m_Socket = new WebSocketSharp.WebSocket(mUrl.ToString());
		m_Socket.OnMessage += (sender, e) => {  m_Messages.Enqueue (e.RawData); };
		m_Socket.OnOpen += (sender, e) => { m_IsConnected = true; };
		m_Socket.OnError += (sender, e) => { Debug.LogError(">>>>> OnError "+e.Message); m_Error = e.Message; };		
		m_Socket.OnClose += (sender, e) => { m_IsConnected = false; };
/*
		m_Socket.Log = new WebSocketSharp.Logger( WebSocketSharp.LogLevel.Trace, null, (WebSocketSharp.LogData data, string str)=>{
			switch( data.Level){
				case WebSocketSharp.LogLevel.Trace:
				case WebSocketSharp.LogLevel.Debug:
				Debug.Log("WebSocketSharp "+data.Message);
				break;
				case WebSocketSharp.LogLevel.Info:
				case WebSocketSharp.LogLevel.Warn:
				Debug.LogWarning("WebSocketSharp "+data.Message);
				break;
				case WebSocketSharp.LogLevel.Error:
				case WebSocketSharp.LogLevel.Fatal:
				Debug.LogError("WebSocketSharp "+data.Message);
				break;
			}
		} ); 
*/
		m_Socket.ConnectAsync();
		while (!m_IsConnected && m_Error == null)
			yield return 0;
	}

	public void DebugReadyState(){
		Debug.Log( ">>>>> "+m_Socket.ReadyState);
	}
	
	public void Send(byte[] buffer) {
		m_Socket.Send(buffer,WebSocketSharp.Opcode.Text );
	}
	public byte[] Recv() {
		if (m_Messages.Count == 0)
			return null;
		return m_Messages.Dequeue();
	}
	public IEnumerator Close() {
		yield return new WaitForEndOfFrame();
		QuickClose();
	}
	public void QuickClose() {
		if(m_IsConnected){
			m_Socket.Close();
			m_IsConnected=false;
		}
	}
	public string Error  {
		get {
			return m_Error;
		}
	}
#endif
}
