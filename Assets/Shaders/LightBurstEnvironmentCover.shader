Shader "Inlighted/LightBurstEnvironmentCover"
{
    Properties
    {
        // Opacity stays exposed so the cover can be softened later if needed,
        // but the first test should use a fully opaque value.
        _Opacity("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "EnvironmentCover"

            Tags
            {
                // Renderer2D expects this pass to identify itself as a 2D render pass.
                // This keeps the environment-cover material on the same rendering
                // path as the 2D sprites whose appearance we are sampling.
                "LightMode" = "Universal2D"
            }

            // The cover must draw over the Burst without changing scene depth.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            /*
             * Renderer2D creates this texture when Camera Sorting Layer Texture
             * is enabled. It contains the scene rendered up to the chosen
             * Foremost Sorting Layer, which lets this mesh redraw the level
             * without also copying the Burst itself.
             */
            TEXTURE2D_X(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)

            float _Opacity;

            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionHCS =
                    TransformObjectToHClip(
                        input.positionOS.xyz
                    );

                // Screen-space UVs are required because the sorting layer texture
                // represents the camera image rather than the mesh's own texture.
                output.screenPos =
                    ComputeScreenPos(
                        output.positionHCS
                    );

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV =
                    input.screenPos.xy /
                    input.screenPos.w;

                // Redrawing the captured environment over the Burst makes the
                // blocked region visually match whatever was behind the wall.
                half4 environmentColour =
                    SAMPLE_TEXTURE2D_X(
                        _CameraSortingLayerTexture,
                        sampler_CameraSortingLayerTexture,
                        screenUV
                    );

                environmentColour.a =
                    _Opacity;

                return environmentColour;
            }

            ENDHLSL
        }
    }
}