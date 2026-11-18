Shader "GameMain/VR HUD Overlay"
{
 Properties
 {
  [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
  _Color ("Tint", Color) = (1,1,1,1)
  _TextureSampleAdd ("Text Texture Add", Vector) = (0,0,0,0)
 }
 SubShader
 {
  Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
  Cull Off ZWrite Off ZTest Always
  Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct v2f {float4 position:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
   sampler2D _MainTex;fixed4 _Color;fixed4 _TextureSampleAdd;
   v2f vert(appdata v)
   {
    v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;
   }
   fixed4 frag(v2f i):SV_Target {return (tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color;}
   ENDCG
  }
 }
}


