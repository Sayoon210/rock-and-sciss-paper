shader_type canvas_item;

// 0이면 온전한 카드, 1이면 조각이 전부 날아간 상태. CardDissolve와 달리 지워지는 선이
// 내려오는 게 아니라, 조각마다 자기 차례에 따로 사라진다.
uniform float progress : hint_range(0.0, 1.0) = 0.0;

// 조각 하나의 픽셀 크기. 작을수록 잘게 부서진다.
uniform float shard_size = 26.0;

// 조각이 사라지기 직전에 밝아지는 색. 이게 없으면 조각이 그냥 꺼지는 것처럼 보이고,
// 부서지는 중이라는 게 안 읽힌다 — CardDissolve의 crumbling_band와 같은 역할이다.
uniform vec4 flash_color : source_color = vec4(1.0, 0.94, 0.78, 1.0);
uniform float flash_lead : hint_range(0.01, 0.5) = 0.15;

// UV가 아니라 로컬 좌표를 쓰는 이유는 CardDissolve와 같다: 카드는 배경/그림/테두리/이름표가
// 각각 다른 노드이고, 이름표는 글자마다 폰트 아틀라스의 UV를 쓴다. UV로 조각을 나누면
// 이름표만 글자 단위로 쪼개진다.
varying vec2 local_position;

void vertex() {
    local_position = VERTEX;
}

// 조각마다 고정된 난수. 같은 조각이면 프레임이 바뀌어도 같은 값이 나와야 조각이
// 깜빡이지 않고 제 차례에 한 번만 사라진다.
float shard_random(vec2 shard) {
    return fract(sin(dot(shard, vec2(127.1, 311.7))) * 43758.5453123);
}

void fragment() {
    vec2 shard = floor(local_position / shard_size);

    // 이 조각이 날아가는 시점. 조각마다 달라야 카드가 한 프레임에 통째로 꺼지지 않고
    // 부서져 흩어지는 것으로 보인다.
    float breaks_at = shard_random(shard);

    if (progress >= breaks_at) {
        discard;
    }

    // 자기 차례가 가까운 조각만 밝아진다. 카드 전체가 아니라 조각 단위로 번쩍이므로
    // 어느 조각이 다음에 날아갈지가 보인다.
    float about_to_break = smoothstep(breaks_at - flash_lead, breaks_at, progress);
    COLOR.rgb = mix(COLOR.rgb, flash_color.rgb, about_to_break * flash_color.a);
}
