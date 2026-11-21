using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Engine;
using Brix.Engine.Nature;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Input;
using Brix.Input.ContextualOrders;
using Brix.Legacy;
using Brix.Lifecycle.Pooling;
using Brix.Lua;
using Brix.Network;
using Brix.Pathfinding.Test;
using Brix.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
public partial class ResourcePackManager : MonoBehaviour
{


	private static object RefGet(object o, string name)
	{
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		Type type = o.GetType();
		PropertyInfo property = type.GetProperty(name, bindingAttr);
		bool flag = property != null;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		object result;
		if (flag8)
		{
			result = property.GetValue(o, null);
		}
		else
		{
			FieldInfo field = type.GetField(name, bindingAttr);
			result = ((field != null) ? field.GetValue(o) : null);
		}
		return result;
	}

	private static void RefSet(object o, string name, object v)
	{
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		Type type = o.GetType();
		PropertyInfo property = type.GetProperty(name, bindingAttr);
		bool flag = property != null && property.CanWrite;
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		if (flag8)
		{
			object value = v;
			bool flag9 = property.PropertyType.IsEnum && v is int;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				value = Enum.ToObject(property.PropertyType, (int)v);
			}
			property.SetValue(o, value, null);
		}
		else
		{
			FieldInfo field = type.GetField(name, bindingAttr);
			bool flag17 = field != null;
			bool flag18 = flag17;
			bool flag19 = flag18;
			bool flag20 = flag19;
			bool flag21 = flag20;
			bool flag22 = flag21;
			bool flag23 = flag22;
			bool flag24 = flag23;
			if (flag24)
			{
				object value2 = v;
				bool flag25 = field.FieldType.IsEnum && v is int;
				bool flag26 = flag25;
				bool flag27 = flag26;
				bool flag28 = flag27;
				bool flag29 = flag28;
				bool flag30 = flag29;
				bool flag31 = flag30;
				bool flag32 = flag31;
				if (flag32)
				{
					value2 = Enum.ToObject(field.FieldType, (int)v);
				}
				field.SetValue(o, value2);
			}
		}
	}

	private static bool RefBool(object o, string name)
	{
		object obj = ResourcePackManager.RefGet(o, name);
		return obj is bool && (bool)obj;
	}

	private static int RefInt(object o, string name)
	{
		object obj = ResourcePackManager.RefGet(o, name);
		return (obj == null) ? 0 : Convert.ToInt32(obj);
	}

}
}
