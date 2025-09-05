using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

public static class BinarySerializer
{
    private static Dictionary<Type, int> sizeCache = new();
    private static Dictionary<Type, FieldInfo[]> fieldsCache = new();

    private static int EstimateObjectSize(Type type)
    {
        if (sizeCache.ContainsKey(type)) return sizeCache[type];

        int size = 0;
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType.IsValueType)
            {
                // For value types, use Unsafe.SizeOf if blittable, otherwise estimate
                if (field.FieldType.IsPrimitive)
                {
                    size += Marshal.SizeOf(field.FieldType);
                }
                else
                {
                    // For custom structs, recursively estimate
                    size += EstimateObjectSize(field.FieldType);
                }
            }
            else // Reference type
            {
                size += IntPtr.Size; // Size of the reference/pointer
            }
        }

        sizeCache[type] = size;

        return size;
    }

    private static FieldInfo[] GetFields(Type type)
    {
        if (fieldsCache.ContainsKey(type)) return fieldsCache[type];

        FieldInfo[] fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.GetCustomAttribute<NonSerializedAttribute>() == null)
            .ToArray();

        fieldsCache.Add(type, fields);
        return fields;
    }

    public static byte[] Serialize(object obj)
    {
        MemoryStream stream = new();
        BinaryWriter writer = new(stream);

        Serialize(writer, obj);

        return stream.ToArray();
    }
    private static void Serialize(BinaryWriter writer, object obj)
    {
        Type type = obj.GetType();

        foreach (FieldInfo fieldInfo in GetFields(type))
        {
            Type t = fieldInfo.FieldType;

            if (t.IsArray)
            {
                Array array = (Array)fieldInfo.GetValue(obj);
                ushort len = (ushort)array.Length;
                writer.Write(len);

                Type elementType = t.GetElementType();

                Debug.Log("Serialize array of " + elementType);

                if (elementType == typeof(byte))
                {
                    byte[] byteArray = (byte[]) array;
                    writer.Write(byteArray);
                }
                else
                {
                    for (int i = 0; i < len; i++)
                    {
                        object elementValue = array.GetValue(i);
                        Write(writer, elementType, elementValue);
                    }
                }
            }
            else
            {
                Write(writer, t, fieldInfo.GetValue(obj));
            }
        }
    }

    private static void Write(BinaryWriter writer, Type t, object value)
    {
        if (t == typeof(int))
        {
            int v = (int)value;
            writer.Write(v);
            // Debug.Log("Write: " + fieldInfo.Name + " = " + value);
        }
        else if (t == typeof(byte))
        {
            byte v = (byte)value;
            writer.Write(v);
        }
        else if (t == typeof(ushort))
        {
            ushort v = (ushort)value;
            // Debug.Log("write: " + fieldInfo.Name + " = " + (ushort)value);
            writer.Write(v);
        }
        else if (t.IsClass)
        {
            Debug.Log("Serialize class " + t.Name);

            Serialize(writer, value);
        }
        else
        {
            throw new NotSupportedException(t.ToString());
        }
    }

    public static T Deserialize<T>(Stream stream)
    {
        return (T)Deserialize(stream, typeof(T));
    }

    public static object Deserialize(Stream stream, Type type)
    {
        BinaryReader reader = new(stream);
        return Deserialize(reader, type);
    }
    public static object Deserialize(BinaryReader reader, Type type)
    {
        object obj = Activator.CreateInstance(type);
        TypedReference _ref = __makeref(obj);

        foreach (FieldInfo fieldInfo in GetFields(type))
        {
            object value;
            Type t = fieldInfo.FieldType;

            if (t.IsArray)
            {
                ushort len = reader.ReadUInt16();
                Type elementType = t.GetElementType();

                // Debug.Log("Derialize array of " + elementType.Name);

                if (elementType == typeof(byte))
                {
                    value = reader.ReadBytes(len);
                }
                else
                {
                    Array arrayObj = Array.CreateInstance(elementType, len);

                    for (int i = 0; i < len; i++)
                    {
                        object elementObj = Read(reader, elementType);
                        arrayObj.SetValue(elementObj, i);
                    }

                    value = arrayObj;
                }
            }
            else
            {
                value = Read(reader, t);
            }

            fieldInfo.SetValueDirect(_ref, value);
        }

        // Debug.Log((obj as MP3EFileHeader).bitRate);

        return obj;
    }

    private static object Read(BinaryReader reader, Type t)
    {
        if (t == typeof(int))
        {
            return reader.ReadInt32();
        }
        else if (t == typeof(byte))
        {
            return reader.ReadByte();
        }
        else if (t == typeof(ushort))
        {
            return reader.ReadUInt16();
        }
        else if (t.IsClass)
        {
            return Deserialize(reader, t);
        }
        else
        {
            throw new NotSupportedException(t.ToString());
        }
    }
}