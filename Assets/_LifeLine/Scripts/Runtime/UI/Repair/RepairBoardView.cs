using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Lifeline
{
    /// <summary>HTML repair boards. Transient widgets; authoritative puzzle/fault data stays in HUD/session.</summary>
    public sealed class RepairBoardView : MonoBehaviour
    {
        public RepairVisualTheme theme;
        public float boardHeight=330;
        public GameSession session;
        public GameHUDController hud;
        public CrewStationPanel panel;
        public CrewActor actor;
        public int selectedWire=-1;
        static readonly int[] RightOrder={2,0,3,1};
        public static readonly string[] CircuitLeft={"接地线","24V 电源","照明支路","无线电支路"};
        public static readonly string[] CircuitRight={"船体地线","蓄电池 +","探照灯","电台"};
        public static readonly string[] CircuitSymbols={"⏚","+","☀","⌁"};
        public static readonly string[] Parts={"燃油软管","点火线圈","传动齿轮","冷却阀门"};
        public static readonly string[] Faults={"燃油管断裂","点火线圈烧毁","传动齿轮崩齿","冷却阀门损坏"};
        public static readonly string[] PartSymbols={"∿","ϟ","⚙","⊗"};
        RectTransform content;
        Image[] oilImages;
        Text[] faultLabels;
        public void Build(StationKind kind)
        {
            if(content!=null){content.gameObject.SetActive(false);Destroy(content.gameObject);}
            var layout=GetComponent<LayoutElement>();if(layout==null)layout=gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight=kind==StationKind.Engine?440:kind==StationKind.Circuit?380:360;
            content=Rect("GeneratedWidgets",transform,Vector2.zero,Vector2.one);
            var bg=content.gameObject.AddComponent<Image>();bg.color=theme!=null?theme.background:new Color(.06f,.15f,.19f);if(theme!=null)bg.sprite=theme.board;
            if(kind==StationKind.Circuit)Circuit();else if(kind==StationKind.Filter)Filter();else Engine();
            UiLocalization.Refresh(gameObject);
        }
        Color Wire(int index)=>theme!=null&&theme.wires.Length>index?theme.wires[index]:Color.cyan;
        void Circuit()
        {
            Label("Instruction",content,"先接地 ⏚，再接其他支路。点击左侧线头，再点右侧对应端口。",new Vector2(.03f,.88f),new Vector2(.97f,.98f),15);
            var wires=Rect("Visible connections",content,new Vector2(.36f,.20f),new Vector2(.64f,.86f)).gameObject.AddComponent<RepairWireGraphic>();
            wires.connected=hud.circuitConnected;wires.colors=theme!=null?theme.wires:new[]{Color.green,Color.yellow,Color.cyan,Color.magenta};wires.raycastTarget=false;
            for(int row=0;row<4;row++)
            {
                int left=row,right=RightOrder[row];float top=.85f-row*.165f;
                var a=Button("Source "+row,content,CircuitLeft[left],new Vector2(.03f,top-.13f),new Vector2(.36f,top),()=>SelectWire(left));
                a.GetComponent<Image>().color=hud.circuitConnected[left]?Wire(left)*.75f:selectedWire==left?new Color(.55f,.46f,.22f):Wire(left)*.3f;
                a.interactable=!hud.circuitConnected[left];CircuitIcon(a,left,false);
                var b=Button("Destination "+right,content,CircuitRight[right],new Vector2(.64f,top-.13f),new Vector2(.97f,top),()=>ConnectWire(right));
                b.GetComponent<Image>().color=hud.circuitConnected[right]?Wire(right)*.75f:Wire(right)*.3f;b.interactable=!hud.circuitConnected[right];CircuitIcon(b,right,true);
            }
            var test=Button("Main breaker",content,"合上总闸 · 恢复供电",new Vector2(.10f,.02f),new Vector2(.90f,.14f),()=>{session.CompleteRepair(StationKind.Circuit);panel.RebuildRepair();});
            test.interactable=Array.TrueForAll(hud.circuitConnected,value=>value);
        }
        void CircuitIcon(Button button,int index,bool right)
        {
            var label=button.GetComponentInChildren<Text>();
            label.rectTransform.anchorMin=new Vector2(right?0:.16f,0);label.rectTransform.anchorMax=new Vector2(right?.84f:1,1);
            var rect=Rect("Replaceable circuit symbol",button.transform,new Vector2(right?.83f:.02f,.25f),new Vector2(right?.97f:.16f,.75f));
            if(theme!=null&&theme.circuitSymbols!=null&&theme.circuitSymbols.Length>index&&theme.circuitSymbols[index]!=null)
            {var image=rect.gameObject.AddComponent<Image>();image.sprite=theme.circuitSymbols[index];image.preserveAspect=true;image.raycastTarget=false;}
            else {var symbol=rect.gameObject.AddComponent<RepairSymbolGraphic>();symbol.symbol=index;symbol.color=Wire(index);symbol.raycastTarget=false;}
        }
        public void SelectWire(int source)
        {
            if(source!=0&&!hud.circuitConnected[0]){session.Tell("请先连接接地线。");return;}
            selectedWire=source;Build(StationKind.Circuit);
        }
        public void ConnectWire(int destination)
        {
            if(selectedWire<0)return;
            if(selectedWire!=destination){session.Tell("端子不匹配。");return;}
            hud.circuitConnected[destination]=true;selectedWire=-1;session.Changed();Build(StationKind.Circuit);
        }
        void Filter()
        {
            var puzzle=hud.filterPuzzle;if(puzzle.masks==null||puzzle.masks.Length==0)puzzle.Generate(session.worldSeed+31);
            bool[] reached=puzzle.FlowCells();
            Label("Instruction",content,"旋转管道：绿色表示已经从入口流通，连接出口后加压试水。",new Vector2(.02f,.88f),new Vector2(.98f,.99f),15);
            for(int row=0;row<puzzle.rows;row++)for(int column=0;column<puzzle.columns;column++)
            {
                int index=row*puzzle.columns+column;
                float x=.13f+column*.148f,y=.84f-row*.175f;
                var tile=Button("Pipe "+index,content,"",new Vector2(x,y-.155f),new Vector2(x+.135f,y),()=>{puzzle.RotateTile(index);Build(StationKind.Filter);});
                tile.GetComponent<Image>().sprite=theme!=null?theme.pipeTile:null;tile.GetComponent<Image>().color=new Color(.11f,.23f,.27f);
                var pipe=Rect("Pipe channels",tile.transform,Vector2.one*.09f,Vector2.one*.91f).gameObject.AddComponent<PipeShapeGraphic>();
                pipe.mask=puzzle.masks[index];pipe.color=reached[index]?(theme!=null?theme.flowing:Color.green):(theme!=null?theme.pipe:Color.gray);pipe.raycastTarget=false;
            }
            float inletY=.84f-puzzle.sourceRow*.175f-.08f,outletY=.84f-puzzle.exitRow*.175f-.08f;
            Label("Inlet",content,"入口 →",new Vector2(0,inletY-.05f),new Vector2(.13f,inletY+.05f),15).color=new Color(.4f,1,.7f);
            Label("Outlet",content,"→ 出口",new Vector2(.87f,outletY-.05f),new Vector2(1,outletY+.05f),15).color=puzzle.IsSolved()?Color.green:new Color(1,.7f,.4f);
            Button("Test pressure",content,"加压试水",new Vector2(.15f,.02f),new Vector2(.85f,.12f),()=>{if(puzzle.IsSolved()){session.CompleteRepair(StationKind.Filter);panel.RebuildRepair();}else session.Tell("尚未连通出口，继续调整。");});
        }
        void Engine()
        {
            Label("Instruction",content,"按住油污擦拭，或按住互动键清理当前故障；露出符号后安装对应零件。",new Vector2(.03f,.91f),new Vector2(.97f,.99f),14);
            var block=Rect("Geometric engine",content,new Vector2(.08f,.52f),new Vector2(.92f,.89f));
            var body=block.gameObject.AddComponent<Image>();body.color=new Color(.26f,.39f,.42f);if(theme!=null)body.sprite=theme.engineBody;
            for(int i=0;i<5;i++){var rib=Rect("Engine rib",block,new Vector2(.05f+i*.19f,.15f),new Vector2(.10f+i*.19f,.85f)).gameObject.AddComponent<Image>();rib.color=new Color(.45f,.58f,.58f);rib.raycastTarget=false;}
            oilImages=new Image[4];faultLabels=new Text[4];
            for(int i=0;i<4;i++)
            {
                int fault=i;float x=i%2==0?.18f:.72f,y=i<2?.68f:.20f;
                var spot=Rect("Fault "+i,block,new Vector2(x-.10f,y-.16f),new Vector2(x+.10f,y+.16f));
                int type=session.engineFaultTypes[i];
                var symbol=Label("Part symbol",spot,PartSymbols[type],Vector2.zero,Vector2.one,32);symbol.color=new Color(1,.8f,.4f);
                if(theme!=null&&theme.engineSymbols.Length>type&&theme.engineSymbols[type]!=null){symbol.enabled=false;var icon=Rect("Replaceable part art",spot,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();icon.sprite=theme.engineSymbols[type];icon.raycastTarget=false;}
                oilImages[i]=Rect("Local oil patch",spot,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();
                oilImages[i].sprite=theme!=null?theme.oilPatch:null;oilImages[i].color=theme!=null?theme.oil:new Color(.1f,.065f,.045f);
                var scrub=oilImages[i].gameObject.AddComponent<EngineOilScrub>();scrub.view=this;scrub.fault=i;
                float top=.49f-i*.065f;
                var select=Button("Select fault "+i,content,"",new Vector2(.03f,top-.055f),new Vector2(.97f,top),()=>{panel.selectedFault=fault;Build(StationKind.Engine);});
                faultLabels[i]=select.GetComponentInChildren<Text>();
            }
            for(int i=0;i<4;i++)
            {
                int part=i;float x=.03f+(i%2)*.48f,top=i<2?.20f:.105f;
                Button("Install part "+i,content,PartSymbols[i]+" "+Parts[i]+" ["+actor.input.GetBindingLabel("Move",i+1)+"]",new Vector2(x,top-.08f),new Vector2(x+.45f,top),()=>{session.TryRepairEngineFault((int)panel.selectedFault,part);panel.RebuildRepair();});
            }
            RefreshEngine();
        }
        void Update(){if(oilImages!=null)RefreshEngine();}
        void RefreshEngine()
        {
            for(int i=0;i<4;i++)
            {
                if(oilImages[i]==null)continue;
                Color oil=theme!=null?theme.oil:new Color(.1f,.065f,.045f);oil.a=1-session.engineFaultClean[i];oilImages[i].color=oil;
                oilImages[i].raycastTarget=!session.engineFaultFixed[i];
                string fault=session.engineFaultFixed[i]?"已修复":session.engineFaultClean[i]>=1?Faults[session.engineFaultTypes[i]]:"油污遮挡";
                faultLabels[i].text=UiLocalization.Text((i+1)+" · "+fault+"  "+session.engineFaultClean[i].ToString("P0"));
            }
        }
        public void Scrub(int index,float seconds){panel.selectedFault=index;panel.ScrubEngineAt(index,seconds);}
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var rect=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;}
        Text Label(string name,Transform parent,string text,Vector2 min,Vector2 max,int size)
        {var t=Rect(name,parent,min,max).gameObject.AddComponent<Text>();t.font=RuntimeUIFont.GetOrCreateFont();t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.text=UiLocalization.Text(text);t.color=theme!=null?theme.ink:Color.white;t.raycastTarget=false;return t;}
        Button Button(string name,Transform parent,string text,Vector2 min,Vector2 max,Action action)
        {var rect=Rect(name,parent,min,max);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.19f,.37f,.39f);if(theme!=null)image.sprite=theme.terminal;var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());Label("Label",rect,text,Vector2.zero,Vector2.one,15);return button;}
    }
}
