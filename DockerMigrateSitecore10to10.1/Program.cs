using System;
using System.IO;
using System.Threading;

namespace DockerMigrateSitecore10to10._1
{
    class Program
    {
        static void Main(string[] args)
        {
            //var optionVal = args[0]; // 1 - to generate files specific to 10.1
            //var docker10Path = args[0]; // 1 - to generate files specific to 10.1

            const string docker10Path = @"C:\Projects\Helix.Examples\examples\helix-basic-tds-consolidated";

            //validate the path to see if there is 10.0 files
            /* does not Modify .env file specific to 10.1
            // modify docker compose file
            // modify docker compose override file
            // Add solr-init folder under build and then add docker file under it
            */

            if (string.IsNullOrWhiteSpace(docker10Path))
            {
                Console.WriteLine("Docker 10 File Path Required!");
                return;
            }

            if (!DockerComposeandOverrideFilesExist(docker10Path))
            {
                Console.WriteLine("Docker 10 Compose and Override Files Required!");
                return;
            }

            UpdateSolrSectionsinDockerComposeFile(docker10Path + @"\docker-compose.yml");// modify docker compose file
            UpdateSolrSectionsinDockerOverrideFile(docker10Path + @"\docker-compose.override.yml"); // modify docker override file
            AddBuildSolrInitFolderandFile(docker10Path + @"\docker\build\solr-init");//add solr-init folder under build folder and then add the required .docker file

        }

        private static bool DockerComposeandOverrideFilesExist(string filePath)
        {
            return Directory.GetFiles(filePath, "docker-compose.yml", SearchOption.TopDirectoryOnly).Length > 0 &&
                   Directory.GetFiles(filePath, "docker-compose.override.yml", SearchOption.TopDirectoryOnly).Length >
                   0;
        }

        private static void UpdateSolrSectionsinDockerComposeFile(string filePath)
        {
            bool fileChanged;
            var concatLines = string.Empty;
            var tracksolr = false;
            var tracksolrinit = false;
            int count = 0;

            using (var input = File.OpenText(filePath))
            using (new StreamWriter("temp.yml"))
            {
                string currline;
                fileChanged = false;
                while (null != (currline = input.ReadLine()))
                {
                    if (currline.Trim().EndsWith("solr:"))
                    {
                        count += 1;
                    }

                    if (!tracksolr)
                    {
                        if (currline.Trim().EndsWith("solr:"))
                        {
                            tracksolr = true;
                        }
                    }
                    else
                    {
                        if (currline.Trim().Contains("image:") && currline.Trim().Contains("solr"))
                        {
                            currline =
                                "    image: ${SITECORE_DOCKER_REGISTRY}nonproduction/solr:8.4.0-${SITECORE_VERSION}";
                            fileChanged = true;
                        }

                        if (currline.Trim().EndsWith("target: c:\\data"))
                        {
                            currline += Environment.NewLine;
                            currline += "    environment:" + Environment.NewLine;
                            currline += "      SOLR_MODE: solrcloud" + Environment.NewLine;
                            currline += "  solr-init:" + Environment.NewLine;
                            currline += "    isolation: ${ISOLATION}" + Environment.NewLine;
                            currline +=
                                "    image: ${SITECORE_DOCKER_REGISTRY}sitecore-xm1-solr-init:${SITECORE_VERSION}" +
                                Environment.NewLine;
                            currline += "    environment:" + Environment.NewLine;
                            currline += "      SITECORE_SOLR_CONNECTION_STRING: http://solr:8983/solr" +
                                        Environment.NewLine;
                            currline += "      SOLR_CORE_PREFIX_NAME: ${SOLR_CORE_PREFIX_NAME}" + Environment.NewLine;
                            currline += "    depends_on:" + Environment.NewLine;
                            currline += "      solr:" + Environment.NewLine;
                            currline += "        condition: service_healthy";
                            fileChanged = true;
                        }

                        if (currline.Trim().Contains("Sitecore_ConnectionStrings_Solr.Search"))
                        {
                            currline += ";solrCloud=true";
                            tracksolrinit = true;
                            fileChanged = true;
                        }

                        if (tracksolrinit)
                        {
                            if (currline.Trim().Contains("SOLR_CORE_PREFIX_NAME"))
                            {
                                currline += Environment.NewLine +
                                            "      MEDIA_REQUEST_PROTECTION_SHARED_SECRET: ${MEDIA_REQUEST_PROTECTION_SHARED_SECRET}";
                                tracksolr = false;
                            }
                        }

                    }

                    if (count > 1) currline = currline.Replace("  solr:", "  solr-init:");
                    concatLines += currline + Environment.NewLine;
                }

            }

            if (fileChanged)
            {
                File.WriteAllText(@"temp1.yml", concatLines);

                File.Replace("temp1.yml", filePath, null);
                File.Delete("temp1.yml");
            }

            Console.WriteLine("Updated Docker Compose File " + filePath);

        }

        private static void UpdateSolrSectionsinDockerOverrideFile(string filePath)
        {
            bool fileChanged;
            var concatLines = string.Empty;
            var trackedLines = string.Empty;
            var tracksolr = false;
            var solrTracked = false;

            using (var input = File.OpenText(filePath))
            using (new StreamWriter("temp.yml"))
            {
                string currline;
                fileChanged = false;
                while (null != (currline = input.ReadLine()))
                {
                    if (currline.Trim().Contains("jss"))
                    {
                        currline = currline.Replace("jss", "sitecore-headless-services");
                        fileChanged = true;
                    }

                    if (!tracksolr)
                    {
                        if (currline.Trim().EndsWith("solr:"))
                        {
                            tracksolr = true;
                        }
                    }
                    else
                    {
                        if (currline.Trim().Contains("image:") && currline.Trim().Contains("solr"))
                        {
                            trackedLines += currline + Environment.NewLine;
                            currline = string.Empty;
                            fileChanged = true;
                        }


                        if (solrTracked)
                        {
                            if (currline.Trim().EndsWith("solr:c:\\data"))
                            {
                                trackedLines = trackedLines.Replace("solr", "solr-init");
                                currline += Environment.NewLine + "  solr-init:" + Environment.NewLine +
                                            trackedLines;
                                solrTracked = false;
                                tracksolr = false;
                            }
                        }
                        else
                        {
                            if (currline.Trim().EndsWith("volumes:"))
                            {
                                solrTracked = true;
                                fileChanged = true;
                            }
                            else
                            {
                                trackedLines += currline + Environment.NewLine;
                                currline = string.Empty;
                                fileChanged = true;
                            }
                        }

                    }


                    concatLines += currline + Environment.NewLine;
                }

            }

            if (fileChanged)
            {

                File.WriteAllText(@"temp1.yml", concatLines);

                File.Replace("temp1.yml", filePath, null);
                File.Delete("temp1.yml");
            }

            Console.WriteLine("Updated Docker Override File " + filePath);
        }

        private static void CheckSolrArgsSectioninDockerOverrideFile(string filePath)
        {
            bool fileChanged;
            var concatLines = string.Empty;
            var tracksolr = false;
            var argsTracked = false;

            using (var input = File.OpenText(@"C:\Projects\Helix.Examples\examples\helix-basic-tds\docker-compose.override.yml"))
            {
                string currline;
                fileChanged = false;
                while (null != (currline = input.ReadLine()))
                {
                    if (!tracksolr)
                    {
                        if (currline.Trim().EndsWith("solr:"))
                        {
                            tracksolr = true;
                        }
                    }
                    else
                    {
                        if (currline.Trim().EndsWith("args:"))
                        {
                            argsTracked = true;
                        }

                        if (argsTracked)
                        {
                            if (currline.Trim().Contains("_IMAGE:"))
                            {
                                var imageName = currline.Trim().Split(':');
                                currline = "# escape=`" + Environment.NewLine + Environment.NewLine;
                                currline += "ARG " + imageName[0].Trim() + Environment.NewLine + Environment.NewLine;
                                currline += "FROM ${" + imageName[0].Trim() + "}";
                                concatLines += currline;
                                fileChanged = true;
                                break;
                            }
                        }

                    }

                }

            }

            if (fileChanged)
            {

                using (var sw = File.CreateText(filePath + @"\.Dockerfile"))
                {
                    sw.WriteLine(concatLines);
                }
            }

            Console.WriteLine("Created Docker File " + filePath);

        }


        private static void AddBuildSolrInitFolderandFile(string filePath)
        {
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }
            CheckSolrArgsSectioninDockerOverrideFile(filePath);
        }
    }
}
