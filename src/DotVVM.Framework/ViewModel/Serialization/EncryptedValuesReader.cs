﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotVVM.Framework.ViewModel.Serialization
{
    public class EncryptedValuesReader
    {
        JsonReader json;
        JsonSerializer serializer;
        Stack<int> propertyIndices = new Stack<int>();
        int virtualNests = 0;
        int propertyIndex = -1;

        public EncryptedValuesReader(JsonReader json)
        {
            this.json = json;
            this.serializer = new JsonSerializer();
        }

        private bool HasProperty(int index)
        {
            var name = index.ToString();
            return virtualNests == 0 && json.TokenType == JsonToken.PropertyName && (string)json.Value == name;
        }

        public void Nest()
        {
            if (HasProperty(propertyIndex))
            {
                json.Read();
                Debug.Assert(json.TokenType == JsonToken.StartObject);
                json.Read();
            }
            else
            {
                virtualNests++;
            }
            propertyIndices.Push(propertyIndex);
            propertyIndex = -1;
        }

        public void AssertEnd()
        {
            if (virtualNests > 0)
            {
                virtualNests--;
                propertyIndex = propertyIndices.Pop();
            }
            else if (json.TokenType == JsonToken.EndObject)
            {
                json.Read();
                propertyIndex = propertyIndices.Pop();
            }
            else throw SecurityError();
        }

        public JToken ReadValue()
        {
            if (json.TokenType == JsonToken.EndArray || virtualNests > 0 || !HasProperty(propertyIndex)) throw SecurityError();
            json.Read();
            var result = JToken.ReadFrom(json);
            json.Read();
            return result;
        }

        public void IncrementIndex()
        {
            propertyIndex++;
        }

        Exception SecurityError() => new SecurityException("Failed to deserialize viewModel encrypted values");

        public static EncryptedValuesReader FromObject(JObject encryptedValues)
        {
            var reader = encryptedValues.CreateReader();
            reader.Read();
            Debug.Assert(reader.TokenType == JsonToken.StartObject);
            reader.Read();
            return new EncryptedValuesReader(reader);
        }
    }
}
