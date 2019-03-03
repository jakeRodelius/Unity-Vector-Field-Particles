//By Jake Rodelius

Shader "Compute/Color Over Life"
{
    Properties
    {
		_StartColor("Start Color", Color) = (1, 0, 0, 1)
		_EndColor("End Color", Color) = (0, 1, 0, 1)
		_Lifetime ("Particle Lifetime", float) = 0
    }
    SubShader
    {

        Pass
        {
			Tags {"Queue" = "Transparent"}
			Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
			#pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

			struct Particle
			{
				float3 position;
				float3 initialPosition;
				float life;
			};

            struct appdata
            {
				uint id : SV_InstanceID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
				half4 color : COLOR;
            };

			half4 _StartColor;
			half4 _EndColor;
			float _Lifetime;

			StructuredBuffer<Particle> particleBuffer;

            v2f vert (appdata p)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(float4(particleBuffer[p.id].position, 1.0f));
				o.color = lerp(_StartColor, _EndColor, particleBuffer[p.id].life / _Lifetime);
				o.color.a = clamp(_Lifetime*0.5f - abs(particleBuffer[p.id].life - (_Lifetime * 0.5f)), 0.0f, 1.0f); //The first and last second of a particles life it fades alpha
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				return i.color;
            }
            ENDCG
        }
    }
}
