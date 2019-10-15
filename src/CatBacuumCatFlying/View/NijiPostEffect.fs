namespace Cbcf.View

open System.Text

module NijiPostEffect =
  [<Literal>]
  let hlsl = """
float g_N;
float g_interval;
float g_speed;
float2 g_resolution;
float g_second;

float3 hsv2rgb(float h, float s, float v) {
    return ((clamp(abs(frac(h+float3(0,2,1)/3.)*6.-3.)-1.,0.,1.)-1.)*s+1.)*v;
}

struct PS_Input
{
    float4 SV_Position : SV_POSITION;
    float4 Position : POSITION;
    float2 UV : UV;
    float4 Color : COLOR;
};

float4 main( const PS_Input Input ) : SV_Target
{
    float2 position = float2(-1.0, 1.0) * ( Input.UV / g_resolution.xy );
    float2 cdn = floor(position * float2(g_N, g_N));
    float index = (cdn.x - 1.0) + cdn.y + g_second * g_speed;

    float h = fmod(index / g_interval, 1.0);
    float3 col = hsv2rgb(h, 1.0, 1.0);
    return float4(col, 1.0);
}
"""
  
  [<Literal>]
  let glsl = """
uniform float g_N;
uniform float g_interval;
uniform float g_speed;
uniform vec2 g_resolution;
uniform float g_second;

vec3 hsv2rgb(float h, float s, float v) {
  return ((clamp(abs(fract(h+float3(0,2,1)/3.)*6.-3.)-1.,0.,1.)-1.)*s+1.)*v;
}

in vec4 inPosition;
in vec2 inUV;
in vec4 inColor;

vec3 main_() {
    vec2 position = vec2(-1.0, 1.0) * ( vec2(inUV.x, 1.0 - inUV.y) / g_resolution.xy );
    vec2 cdn = floor(position * vec2(g_N, g_N));
    float index = (cdn.x - 1.0) + cdn.y + g_second * g_speed;

    float h = fmod(index / g_interval, 1.0);
    vec3 col = hsv2rgb(h, 1.0, 1.0);
    return col;
}

void main() {
    outOutput = vec4(main_(), 1.0);
}

"""

type NijiPostEffect() =
  inherit asd.PostEffect()

  let shader =
    asd.Engine.Graphics.GraphicsDeviceType
    |> function
    | asd.GraphicsDeviceType.DirectX11 ->
      NijiPostEffect.hlsl
    | asd.GraphicsDeviceType.OpenGL ->
      NijiPostEffect.glsl
    | _ -> raise (System.NotSupportedException("Not supported graphics device"))
    |> asd.Engine.Graphics.CreateShader2D

  let material2d = asd.Engine.Graphics.CreateMaterial2D(shader)

  do
    material2d.SetFloat("g_N", 30.0f)
    material2d.SetFloat("g_interval", 50.0f)
    material2d.SetFloat("g_speed", 10.0f)
    let ws = asd.Engine.WindowSize.To2DF()
    material2d.SetVector2DF("g_resolution", ws / ws.X)

  let mutable second = 0.0f

  override this.OnDraw(dst, _src) =
    material2d.SetFloat("g_second", second)
    second <- second + 1.0f / asd.Engine.CurrentFPS

    this.DrawOnTexture2DWithMaterial(dst, material2d)
