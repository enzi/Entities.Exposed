﻿namespace Unity.Entities.Exposed
{
    public static unsafe class BlobExtension
    {
        public static int GetLength<T>(this BlobAssetReference<T> blob)
            where T : unmanaged
        {
            return blob.m_data.Header->Length;
        }
    }
}