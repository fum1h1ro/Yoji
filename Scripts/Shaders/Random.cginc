inline int LFSR_Rand_Gen(in int n)
{
    // <<, ^ and & require GL_EXT_gpu_shader4.
    n = (n << 13) ^ n; 
    return (n * (n*n*15731+789221) + 1376312589) & 0x7fffffff;
}

