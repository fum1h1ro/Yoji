using System;

namespace Yoji.Runtime
{
    [Serializable]
    public struct SubMesh
    {
        public int IndexStart;
        public int IndexCount;

        public SubMesh(int indexStart, int indexCount)
        {
            IndexStart = indexStart;
            IndexCount = indexCount;
        }
    }

    public struct ReadonlySubMesh
    {
        public readonly int IndexStart;
        public readonly int IndexCount;

        public ReadonlySubMesh(int indexStart, int indexCount)
        {
            IndexStart = indexStart;
            IndexCount = indexCount;
        }
    }
}
