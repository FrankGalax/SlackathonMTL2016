using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SlackathonMTL.Model
{
    public class Person
    {
        public string Id { get; set; }
        public string Username { get; set; }

        private static List<Person> m_persons;
        private static string m_path;

        static Person()
        {
            string appPath = HttpRuntime.AppDomainAppPath;
            m_path = Path.Combine(appPath, "Data", "persons.json");
            m_persons = new List<Person>();
        }

        public static void Load()
        {
            m_persons.Clear();

            string json = System.IO.File.ReadAllText(m_path);

            JArray jArray = JArray.Parse(json);
            foreach (var item in jArray.Children())
            {
                dynamic p = JsonConvert.DeserializeObject(item.ToString());
                Person person = new Person
                {
                    Id = p.Id,
                    Username = p.Username
                };
                m_persons.Add(person);
            }
        }

        public static void Save()
        {
            JsonSerializer serializer = new JsonSerializer();

            using (StreamWriter sw = new StreamWriter(m_path))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Formatting.Indented;
                serializer.Serialize(writer, m_persons);
            }
        }

        public static void Add(Person person)
        {
            m_persons.Add(person);
        }

        public static void Update(Person person)
        {
            Person p = m_persons.FirstOrDefault(pe => pe.Id == person.Id);
            if (p == null)
                return;
            p.Username = person.Username;
        }

        public static List<Person> GetAll()
        {
            return m_persons;
        }
    }
}