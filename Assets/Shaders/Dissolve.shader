Shader "Custom/Dissolve"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        
        _DissolveTex ("Dissolve Noise (Grayscale)", 2D) = "white" {}
        _DissolveAmount ("Dissolve Progress", Range(0.0, 1.0)) = 0.0
        
        _EdgeWidth ("Edge Width", Range(0.0, 0.5)) = 0.1
        _EdgeColor ("Edge Color (Glow)", Color) = (0, 1, 1, 1) // Neon Cyan
    }
    SubShader
    {
        // Standard Surface Shader
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
            // Get dissolve value
            float dissolveVal = tex2D(_DissolveTex, IN.uv_DissolveTex).r;
            float isVisible = dissolveVal - _DissolveAmount;
            
            // Discard pixel if below dissolve value
            clip(isVisible);

            // Glow edge effect
            if (isVisible < _EdgeWidth)
            {
                // Bloom intensity multiplier (5.0)
                o.Emission = _EdgeColor.rgb * (1.0 - (isVisible / _EdgeWidth)) * 5.0; 
            }

            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
