using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace SlackathonMTL.Model
{
    public class Matrix
    {
        private static Dictionary<int, Dictionary<int, int>> m_personSubjectPoints;
        private static string m_path;

        static Matrix()
        {
            m_personSubjectPoints = new Dictionary<int, Dictionary<int, int>>();
            string appPath = HttpRuntime.AppDomainAppPath;
            m_path = Path.Combine(appPath, "Data", "matrix.json");
        }

        public static void Load()
        {
            List<Person> persons = Person.GetAll();
            List<Subject> subjects = Subject.GetAll();

            m_personSubjectPoints.Clear();

            foreach (Person person in persons)
            {
                m_personSubjectPoints.Add(person.Id, new Dictionary<int, int>());
                foreach (Subject subject in subjects)
                {
                    m_personSubjectPoints[person.Id].Add(subject.Id, 0);
                }
            }

            string json = System.IO.File.ReadAllText(m_path);

            JArray jArray = JArray.Parse(json);
            foreach (var item in jArray.Children())
            {
                MatrixEntry m = JsonConvert.DeserializeObject<MatrixEntry>(item.ToString());

                Person person = persons.FirstOrDefault(pe => pe.Id == m.PersonId);
                Subject subject = subjects.FirstOrDefault(s => s.Id == m.SubjectId);

                if (person == null)
                {
                    Console.Error.WriteLine("La personne avec l'id == {0} est pas du tout", m.PersonId);
                    continue;
                }

                if (subject == null)
                {
                    Console.Error.WriteLine("Le sujet avec l'id == {0} est pas du tout", m.PersonId);
                    continue;
                }

                m_personSubjectPoints[person.Id][subject.Id] = m.Points;
            }
        }

        public static void Save()
        {
            List<MatrixEntry> matrixEntries = new List<MatrixEntry>();

            foreach (var personEntry in m_personSubjectPoints)
            {
                foreach (var subjectEntry in personEntry.Value)
                {
                    if (subjectEntry.Value == 0)
                        continue;

                    matrixEntries.Add(new MatrixEntry
                    {
                        PersonId = personEntry.Key,
                        SubjectId = subjectEntry.Key,
                        Points = subjectEntry.Value
                    });
                }
            }
            JsonSerializer serializer = new JsonSerializer();

            using (StreamWriter sw = new StreamWriter(m_path))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Formatting.Indented;
                serializer.Serialize(writer, matrixEntries);
            }
        }

        public static int GetPoints(Person person, Subject subject)
        {
            if (!m_personSubjectPoints.ContainsKey(person.Id))
                return 0;

            if (!m_personSubjectPoints[person.Id].ContainsKey(subject.Id))
                return 0;

            return m_personSubjectPoints[person.Id][subject.Id];
        }

        public static void SetPoints(Person person, Subject subject, int points)
        {
            if (!m_personSubjectPoints.ContainsKey(person.Id))
            {
                m_personSubjectPoints.Add(person.Id, new Dictionary<int, int>());
            }

            if (!m_personSubjectPoints[person.Id].ContainsKey(subject.Id))
            {
                m_personSubjectPoints[person.Id].Add(subject.Id, points);
            }
            else
            {
                m_personSubjectPoints[person.Id][subject.Id] = points;
            }
        }
    }
}