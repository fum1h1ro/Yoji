typedef float4 quat;

inline quat quat_mul(quat q1, quat q2)
{
    float3 xyz = cross(q1.xyz, q2.xyz) + q2.w * q1.xyz + q1.w * q2.xyz;
    float w = q1.w * q2.w - dot(q1.xyz, q2.xyz);
    return quat(xyz, w);
}

inline quat quat_axis_angle(float3 axis, float angle)
{
    float ha = angle * 0.5;
    float s, c;
    sincos(ha, s, c);
    float4 r;
    r.x = axis.x * s;
    r.y = axis.y * s;
    r.z = axis.z * s;
    r.w = c;
    return r;
}

inline quat quat_conj(quat q)
{
    q.xyz *= -1.0;
    return q;
}

inline float3 quat_transform(quat q, float3 p)
{
    quat tmp = quat_mul(q, float4(p, 0));
    quat qj = quat_conj(q);
    return quat_mul(tmp, qj).xyz;
}

