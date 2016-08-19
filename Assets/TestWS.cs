using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using MiniJSON;

public class TestWS : MonoBehaviour {
	WebSocketWrapper ws;
    string FireBaseServer = "glaring-torch-9586";
    int version = 5;
	// Use this for initialization
	void Start () {
		ws = new WebSocketWrapper(string.Format("wss://{0}.firebaseio.com/.ws?v={1}", FireBaseServer, version));
		StartCoroutine(ws.Connect());
	}
	
    void Update(){
        if(ws!=null)
            ParseFireBaseCommand(ws.RecvString());
    }

    void ParseFireBaseCommand(string command){
        if(command==null) return;
        var message = Json.Deserialize(command) as Dictionary<string,object>;
        var data = message["d"] as Dictionary<string,object>;
        if( message["t"] as string == "c") OnControl(data);
        else OnData(data);
    }

    void OnControl(Dictionary<string,object> json){
        if(json.ContainsKey("d")){
            var type = json["t"] as string;
            var data = json["d"];
            switch(type){
                case "h": OnHello(data as Dictionary<string,object>); break;
                case "r": OnControlReset(data as string); break;
                case "e": OnError(data as string); break; // Control error!
//                case "n": break;// End Trasmision.
//                case "s": break;// Control shutdown.
//                case "o": break;// Control pong!
                default:
                    Debug.LogError("Comando "+type+" desconocido.");
                    break;
            }
        }
    }

    void OnError(string error) {
        Debug.LogError(">>>>> "+error);
    }

    void OnHello(Dictionary<string,object> payload){        
        var timestamp = (long)payload["ts"];
        var version = payload["v"] as string;
        var host = payload["h"] as string;
        var sessionId = payload["s"] as string;
        WriteCommand("s",  Json.Deserialize("{\"c\":{\"sdk.js.2-2-9\":1,\"framework.cordova\":1}}") as Dictionary<string,object>); 
        AuthByToken("GHlim3fJZbq5NY5tkQPUYlqfMrc63b9Oq5WQTVKt");
                            
        PutNode("/sessions/Dinosaurios", "'test android'");
        SimpleListen("/sessions/Dinosaurios");
    }

    void OnControlReset(string data){
        ws.Close();
        ws = new WebSocketWrapper(string.Format( "wss://{0}/.ws?v={1}&ns={2}",data as string, version, FireBaseServer));            
        StartCoroutine(ws.Connect());
    }

    void OnData(Dictionary<string,object> json){
        var b = json.ContainsKey("b")?json["b"]:null;
        var r = (long)(json.ContainsKey("r")?json["r"]:0L);
        var a = json.ContainsKey("a")?json["a"] as string:"";
        var error = json.ContainsKey("error")?json["error"]:null;
        if(r!=0) OnResponse(r, b);
        else if(error!=null){
        }else {
            OnAction(a,b);
        }
    }

    void OnResponse(long id, object body){
//        Debug.Log("OnResponse("+id+")");
    }

    void OnAction(string action, object body){
        // action d,m,c,ac,sd
        switch(action){
            case "d":
                Debug.Log(">>>> OnAction("+action+") data "+Json.Serialize(body));
                break;
            default:
                Debug.LogError("Accion "+action+" desconocida.");
                break;
        }
    }

    void WriteCommand(string a, Dictionary<string,object> data){
        WriteCommand(a, Json.Serialize(data));
    }

    long requestID = 1; 
    void WriteCommand(string a, string data){
        // r: requestID, a: action, b: body
        ws.SendString(string.Format( "{{'t':'d','d':{{'r':{0},'a':'{1}','b':{2}}}}}", requestID++, a, data).Replace('\'','"'));
    }

    public void AuthByToken(string token){
        // Command: 'auth' Params: cred: token
        WriteCommand( "auth", string.Format("{{'cred':'{0}'}}",token) );
    }

    public void SimpleListen(string path ){ // Falata el delegado para las modificaciones.
        // Command: 'q' Params: p: path, t: ???, h: ???
        WriteCommand( "q", string.Format("{{'p':'{0}'}}",path) );
    } 

    public void PutNode(string path, string val){
        WriteCommand("p", string.Format("{{'p':'{0}','d':{1}}}",path, val) );
    }

    public void MergeNode(string path, string val){
        WriteCommand("m", string.Format("{{'p':'{0}','d':{1}}}",path, val) );
    }

}