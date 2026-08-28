shader_type canvas_item;

// 0이면 온전한 카드, 1이면 완전히 사라진 상태. 지워지는 선이 카드 위에서 아래로 내려간다.
uniform float progress : hint_range(0.0, 1.0) = 0.0;

// 지워지는 경계가 얼마나 울퉁불퉁한지. 0이면 자를 대고 그은 직선이 그대로 내려간다.
uniform float edge_roughness : hint_range(0.0, 1.0) = 0.35;

// 아직 남아 있지만 곧 지워질 띠의 두께와 색. 경계가 "부서지는 중"이라는 걸 읽히게 하는
// 부분이라, 이게 없으면 카드가 부서지는 게 아니라 잘려 나가는 것처럼 보인다.
uniform float crumbling_band : hint_range(0.0, 0.5) = 0.05;
uniform vec4 crumbling_color : source_color = vec4(0.88, 0.90, 0.97, 1.0);

// 카드의 세로 픽셀 크기. 자식 노드가 저마다 자기 로컬 좌표를 쓰므로, 같은 높이로 나눠야
// 배경과 그림과 테두리가 같은 선에서 같이 지워진다.
uniform float card_height = 280.0;

// 다 사라졌을 때 카드가 도달해 있는 밝기. 1이면 끝까지 원래 밝기 그대로다. 남은 부분이
// 마지막까지 멀쩡한 밝기면 무엇이 사라지는 중인지 한눈에 안 들어온다.
uniform float dim_to : hint_range(0.0, 1.0) = 0.2;

uniform sampler2D noise_texture : repeat_enable, filter_linear;
uniform float noise_scale = 0.02;

// UV가 아니라 로컬 좌표를 쓰는 이유: 카드는 배경/그림/테두리/이름표가 각각 다른 노드이고,
// 그중 Label은 글자마다 폰트 아틀라스의 UV를 쓴다. UV로 계산하면 이름표만 카드와 무관하게
// 글자 단위로 지워진다.
varying vec2 local_position;

void vertex() {
    local_position = VERTEX;
}

void fragment() {
    float noise = texture(noise_texture, local_position * noise_scale).r;

    // 이 픽셀이 사라질 시점. 노이즈가 없으면 세로 위치 그대로라 가로 직선이 내려간다.
    float dissolves_at = (local_position.y / card_height) + (noise - 0.5) * edge_roughness;

    // 노이즈가 경계를 위아래로 edge_roughness의 절반씩 밀어내므로, 선의 시작과 끝도 그만큼
    // 바깥에서 잡는다. 안 그러면 progress가 0인데 위쪽이 이미 조금 뜯겨 있고, 1인데 아래쪽에
    // 부스러기가 남는다.
    float front = mix(-edge_roughness * 0.5, 1.0 + edge_roughness * 0.5, progress);

    if (dissolves_at < front) {
        discard;
    }

    // 어두워지는 것이 먼저다. 부서지는 띠는 그 위에 얹혀야 어두워지는 카드에 대비되어
    // 밝게 남는다.
    COLOR.rgb *= mix(1.0, dim_to, progress);

    float crumbling = 1.0 - smoothstep(front, front + crumbling_band, dissolves_at);
    COLOR.rgb = mix(COLOR.rgb, crumbling_color.rgb, crumbling * crumbling_color.a);
}
