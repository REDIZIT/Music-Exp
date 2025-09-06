Shader "Custom/BufferShader"
{
    Properties
    {
        // Цвета для активного и неактивного состояния
        _ColorActive ("Active Color", Color) = (1, 1, 1, 1)
        _ColorInactive ("Inactive Color", Color) = (0.2, 0.2, 0.2, 1)
    }
    SubShader
    {
        // Не отсекаем грани, не пишем в буфер глубины, тест глубины всегда проходит
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Используем более современную модель шейдеров для поддержки циклов
            #pragma target 3.0

            #include "UnityCG.cginc"

            // Определяем максимальное количество сегментов.
            // Это значение должно совпадать с тем, что в C# скрипте.
            #define MAX_SEGMENTS 128

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            // Переменные, которые мы будем устанавливать из C# скрипта
            fixed4 _ColorActive;
            fixed4 _ColorInactive;
            int _NumSegments; // Фактическое количество сегментов для обработки

            // Массивы с данными. bool представлен как float (0.0 = false, 1.0 = true)
            float _Segments[MAX_SEGMENTS];
            int _StartWithActive;


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float x = i.uv.x;

                float current_segment_border = 0.0;

                fixed4 finalColor = _ColorInactive;

                [loop]
                for (int j = 0; j < _NumSegments; j++)
                {
                    current_segment_border += _Segments[j];

                    if (x <= current_segment_border)
                    {
                        if (j % 2 == _StartWithActive)
                        {
                            finalColor = _ColorInactive;
                        }
                        else
                        {
                            finalColor = _ColorActive;
                        }
                        break;
                    }
                }

                return finalColor;
            }
            ENDCG
        }
    }
}