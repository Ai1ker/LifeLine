using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Connects the repair views to the authoritative puzzle and fault state.</summary>
    public sealed class RepairBoardView : MonoBehaviour
    {
        public RepairVisualTheme theme;
        public EngineRepairLayout engineLayout;
        public FilterRepairLayout filterLayout;
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
        public void Build(StationKind kind)
        {
            if (content != null)
            {
                content.gameObject.SetActive(false);
                Destroy(content.gameObject);
                content = null;
            }
            var layout = GetComponent<LayoutElement>();
            if (layout == null) layout = gameObject.AddComponent<LayoutElement>();
            if (engineLayout != null) engineLayout.gameObject.SetActive(kind == StationKind.Engine);
            if (filterLayout != null) filterLayout.gameObject.SetActive(kind == StationKind.Filter);
            switch (kind)
            {
                case StationKind.Engine:
                    if (engineLayout != null && theme != null)
                    {
                        layout.preferredHeight = FitBoardHeight(engineLayout.preferredHeight);
                        engineLayout.Bind(this);
                    }
                    break;
                case StationKind.Filter:
                    if (filterLayout != null && theme != null)
                    {
                        layout.preferredHeight = FitBoardHeight(filterLayout.preferredHeight);
                        filterLayout.Bind(this);
                    }
                    break;
                case StationKind.Circuit:
                    layout.preferredHeight = 380;
                    content = Rect("Circuit widgets", transform, Vector2.zero, Vector2.one);
                    var backdrop = content.gameObject.AddComponent<Image>();
                    backdrop.sprite = theme != null ? theme.board : null;
                    backdrop.color = theme != null ? theme.background : new Color(.06f, .15f, .19f);
                    Circuit();
                    break;
            }
            UiLocalization.Refresh(gameObject);
        }

        float FitBoardHeight(float preferredHeight)
        {
            var scroll = GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.viewport == null || scroll.viewport.rect.height <= 0) return preferredHeight;
            var group = transform.parent.GetComponent<VerticalLayoutGroup>();
            float padding = group != null ? group.padding.vertical : 0;
            // Keep the controls usable in a very short viewport and let it scroll there.
            float available = Mathf.Max(300, Mathf.Floor(scroll.viewport.rect.height - padding));
            return Mathf.Min(preferredHeight, available);
        }

        public void Bind(string name, Button button, Action action)
        {
            button.onClick.RemoveAllListeners();
            panel.BindOnlineAction(button, "r:" + name, action);
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
            hud.circuitConnected[destination]=true;selectedWire=-1;
            GameAudio.Global(session, AudioCue.RepairWireConnect, actor);
            session.Changed();Build(StationKind.Circuit);
        }
        public void Scrub(int index,float seconds){panel.selectedFault=index;panel.ScrubEngineAt(index,seconds);}
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var rect=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;}
        Text Label(string name,Transform parent,string text,Vector2 min,Vector2 max,int size)
        {var t=Rect(name,parent,min,max).gameObject.AddComponent<Text>();t.font=RuntimeUIFont.GetOrCreateFont();t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.text=UiLocalization.Text(text);t.color=theme!=null?theme.ink:Color.white;t.raycastTarget=false;return t;}
        Button Button(string name,Transform parent,string text,Vector2 min,Vector2 max,Action action)
        {var rect=Rect(name,parent,min,max);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.19f,.37f,.39f);if(theme!=null)image.sprite=theme.terminal;var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;panel.BindOnlineAction(button,"r:"+name,action);Label("Label",rect,text,Vector2.zero,Vector2.one,15);return button;}
    }
}
