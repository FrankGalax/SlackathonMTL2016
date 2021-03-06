﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace SlackathonMTL.Model
{
    public class Subject
    {
        public string Id { get; set; }
        public string Name { get; set; }

        private static List<Subject> m_subjects;
        private static string m_path;

        static Subject()
        {
            string appPath = HttpRuntime.AppDomainAppPath;
            m_path = Path.Combine(appPath, "Data", "subjects.json");
            m_subjects = new List<Subject>();
        }

        public static void Load()
        {
            m_subjects.Clear();

            string json = System.IO.File.ReadAllText(m_path);

            JArray jArray = JArray.Parse(json);
            foreach (var item in jArray.Children())
            {
                dynamic p = JsonConvert.DeserializeObject(item.ToString());
                Subject subject = new Subject
                {
                    Id = p.Id,
                    Name = p.Name
                };
                m_subjects.Add(subject);
            }
        }

        public static void Save()
        {
            JsonSerializer serializer = new JsonSerializer();

            using (StreamWriter sw = new StreamWriter(m_path))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Formatting.Indented;
                serializer.Serialize(writer, m_subjects);
            }
        }

        public static void Add(Subject subject)
        {
            m_subjects.Add(subject);
        }

        public static List<Subject> GetAll()
        {
            return m_subjects;
        }
    }
}