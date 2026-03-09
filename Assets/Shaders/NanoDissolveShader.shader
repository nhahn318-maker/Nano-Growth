Shader "Custom/NanoDissolve"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0.0, 1.0)) = 0.0
        _DissolveTex ("Dissolve Guide (Noise)", 2D) = "white" {}
        [HDR] _EdgeColor ("Edge Color (Neon)", Color) = (0, 1, 1, 1)
        _EdgeWidth ("Edge Width", Range(0.0, 0.1)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _DissolveTex;
        float _DissolveAmount;
        float _EdgeWidth;
        fixed4 _EdgeColor;
        fixed4 _Color;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_DissolveTex;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Lấy giá trị từ texture noise
            float dissolveVal = tex2D(_DissolveTex, IN.uv_DissolveTex).r;
            
            // Chỉ đục lỗ khi giá trị tan biến lớn hơn 0
            if (_DissolveAmount > 0.001) {
                clip(dissolveVal - _DissolveAmount);
            }

            // Hiệu ứng viền sáng (Chỉ hiện khi đang tan biến)
            float edge = 0;
            if (_DissolveAmount > 0.001 && _DissolveAmount < 0.99) {
                edge = step(dissolveVal - _DissolveAmount, _EdgeWidth);
            }
            o.Emission = edge * _EdgeColor.rgb * 5.0; 

            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
