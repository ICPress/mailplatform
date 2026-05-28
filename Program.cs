// See https://aka.ms/new-console-template for more information
using System.Text;
using System.Text.Json;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;

var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"); // DOTNET_ENVIRONMENT
IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{environmentName}.json", true, true)
        .Build();

ServerSettings? serverSettings = configuration.GetRequiredSection("ServerSettings").Get<ServerSettings>();
if (serverSettings == null)
{
    Console.WriteLine("Appsettings is not initialized!");
    return;
}
// Configure the HTTP request pipeline.
Console.WriteLine("Running environment:" + environmentName + ", gorse:" + serverSettings.GorseAPIEndpoint);

var queueList = new List<EmalQueueRow>(50);


Console.WriteLine("Mailplatform (Storypop) started!");
try
{
    while (true)
    {
        using (MySqlConnection connection = new(serverSettings.MysqlConnectionStoryPop))
        {
            try
            {
                await connection.OpenAsync();
                var mySqlCommand = new MySql.Data.MySqlClient.MySqlCommand();
                mySqlCommand.CommandText = "SELECT queue_id, email , type ,  additional_data , claimed  from mail_queue order by queue_id limit 50";
                mySqlCommand.Connection = connection;
                using (var reader = mySqlCommand.ExecuteReader())
                {
                    try
                    {
                        while (await reader.ReadAsync())
                        {
                            var queueRow = new EmalQueueRow();
                            queueRow.queue_id = reader.GetUInt32("queue_id");
                            queueRow.email = reader.GetString("email");
                            queueRow.additional_data = reader.IsDBNull(reader.GetOrdinal("additional_data")) ? null : reader.GetString("additional_data");
                            queueRow.type = (EmailType)reader.GetUInt16("type"); ;
                            queueRow.claimed = reader.GetBoolean("claimed");
                            if (queueRow.claimed) continue;
                            else queueList.Add(queueRow);
                        }
                    }
                    finally
                    {
                        await reader.CloseAsync();
                    }
                }
                foreach (EmalQueueRow eventRow in queueList.Where(x => !x.claimed))
                {
                    try
                    {
                        var mySqlCommand2 = new MySql.Data.MySqlClient.MySqlCommand();
                        mySqlCommand2.CommandText = "UPDATE mail_queue SET claimed=1  WHERE queue_id=@queue_id and claimed = 0";
                        mySqlCommand2.Connection = connection;
                        mySqlCommand2.Parameters.AddWithValue("@queue_id", eventRow.queue_id);
                        if ((long?) await mySqlCommand2.ExecuteNonQueryAsync() != 0)
                        {
                            if (eventRow.type == EmailType.REGISTER || eventRow.type == EmailType.RECOVER_SIGN_ON)
                            {
                                if (eventRow.additional_data == null)
                                {
                                    Console.WriteLine("ERROR: missing additional data for register token, email:" + eventRow.email);
                                }
                                else await MailUtil.SendEmailAccessToken(eventRow.email, eventRow.additional_data, serverSettings);
                            }
                            else if (eventRow.type == EmailType.INVITE)
                            {
                                if (eventRow.additional_data == null)
                                {
                                    Console.WriteLine("ERROR: missing additional data for register token, email:" + eventRow.email);
                                }
                            }
                            else if (eventRow.type == EmailType.CLEAR_DATA_REQUEST)
                            {
                               await MailUtil.SendEmailDeleteMyData(eventRow.email, serverSettings);
                            }
                            else if (eventRow.type == EmailType.TRANSFER_REQUEST_FULFILLED)
                            {
                                if (eventRow.additional_data == null)
                                {
                                    Console.WriteLine("ERROR: missing additional data for transfer_request_id, email:" + eventRow.email);
                                }
                                else
                                {
                                    var transfer_request_id = int.Parse(eventRow.additional_data);
                                    var transferWallet = new MySql.Data.MySqlClient.MySqlCommand();
                                    transferWallet.CommandText = "select wallet_address from claim_transfer_request  WHERE transfer_request_id=@transfer_request_id";
                                    transferWallet.Connection = connection;
                                    transferWallet.Parameters.AddWithValue("@transfer_request_id", transfer_request_id);
                                    var wallet_address = (string)transferWallet.ExecuteScalar();
                                    var displayWallet = wallet_address.Substring(0, 4) + "...." + wallet_address.Substring(wallet_address.Length - 4 - 1, 4);
                                    var strb = new StringBuilder(File.ReadAllText("email_transfer_request.html"));
                                    var transferItems = new MySql.Data.MySqlClient.MySqlCommand();
                                    transferItems.CommandText = "select count(*) as amount,rew.name, ti.transfer_request_id, destination_account_address, signature  from claim_transfer_request_item ti inner join rewards rew on rew.reward_id = ti.reward_id where ti.transfer_request_id = @transfer_request_id group by rew.name, ti.transfer_request_id, destination_account_address, signature";
                                    transferItems.Connection = connection;
                                    transferItems.Parameters.AddWithValue("@transfer_request_id", transfer_request_id);
                                    using (var reader = transferItems.ExecuteReader())
                                    {
                                        try
                                        {
                                            var strbItems = new StringBuilder();
                                            while (await reader.ReadAsync())
                                            {
                                                var strbItem = new StringBuilder(File.ReadAllText("email_transfer_request_item.html"));
                                                strbItem.Replace("{{rewardName}}", reader.GetString("name"));
                                                strbItem.Replace("{{displayAmount}}", reader.GetUInt32("amount").ToString());
                                                var transactionSignatureFull = reader.GetString("signature");
                                                strbItem.Replace("{{displayTransaction}}", transactionSignatureFull.Substring(0, 6) + "....");
                                                strbItem.Replace("{{transaction}}", transactionSignatureFull);
                                                strbItems.Append(strbItem.ToString());
                                            }
                                            strb.Replace("{{displayWallet}}", displayWallet);
                                            strb.Replace("{{wallet}}", wallet_address);
                                            strb.Replace("{{itemsTransfered}}", strbItems.ToString());
                                            strb.Replace("{{static_path}}", serverSettings.ImageStaticPath);
                                        }
                                        finally
                                        {
                                            await reader.CloseAsync();
                                        }
                                    }
                                    var toSend = strb.ToString();
                                    await MailUtil.SendEmailWithBody(eventRow.email, toSend, "Reward Transfer Request Completed", serverSettings);
                                    await MailUtil.SendEmailWithBody(serverSettings?.SMTPUsername ?? "contect@storypop.net", toSend, "["+ transfer_request_id.ToString()+"] Reward Transfer Request Completed", serverSettings);
                                    var mySqlCommandTransFinish = new MySql.Data.MySqlClient.MySqlCommand();
                                    mySqlCommandTransFinish.CommandText = "UPDATE claim_transfer_request SET fullfilled_at = CURRENT_TIMESTAMP WHERE transfer_request_id=@transfer_request_id ";
                                    mySqlCommandTransFinish.Connection = connection;
                                    mySqlCommandTransFinish.Parameters.AddWithValue("@transfer_request_id", transfer_request_id);
                                    await mySqlCommandTransFinish.ExecuteNonQueryAsync();
                                    mySqlCommandTransFinish = new MySql.Data.MySqlClient.MySqlCommand();
                                    mySqlCommandTransFinish.CommandText = "UPDATE user_claimed_rewards SET transfered_at = CURRENT_TIMESTAMP WHERE transfer_request_id=@transfer_request_id ";
                                    mySqlCommandTransFinish.Connection = connection;
                                    mySqlCommandTransFinish.Parameters.AddWithValue("@transfer_request_id", transfer_request_id);
                                    await mySqlCommandTransFinish.ExecuteNonQueryAsync();
                                }
                            } else if (eventRow.type == EmailType.ACCOUNT_TERMINATION_NOTICE)
                            {
                                var strb = new StringBuilder(File.ReadAllText("email_account_termination_notice.html"));
                                strb.Replace("{{username}}", eventRow.additional_data);
                                strb.Replace("{{static_path}}", serverSettings.ImageStaticPath);
                                var toSend = strb.ToString();
                                await MailUtil.SendEmailWithBody(eventRow.email, toSend, "Account Termination Notice", serverSettings);
                            }else if (eventRow.type == EmailType.ARTICLE_DELETED)
                            {
                                var strb = new StringBuilder(File.ReadAllText("article_removal_notice.html"));
                                strb.Replace("{{username}}", eventRow.additional_data);
                                strb.Replace("{{static_path}}", serverSettings.ImageStaticPath);
                                var toSend = strb.ToString();
                                await MailUtil.SendEmailWithBody(eventRow.email, toSend, "Article Removed Notice", serverSettings);
                            }
                            var mySqlCommandFinish = new MySql.Data.MySqlClient.MySqlCommand();
                            mySqlCommandFinish.CommandText = "DELETE FROM mail_queue WHERE queue_id=@queue_id";
                            mySqlCommandFinish.Connection = connection;
                            mySqlCommandFinish.Parameters.AddWithValue("@queue_id", eventRow.queue_id);
                            await mySqlCommandFinish.ExecuteNonQueryAsync();
                        }
                        else continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error occured when handling email queue, queueid:" + eventRow.queue_id + ", error:" + ex.Message);
                        var mySqlCommand2 = new MySql.Data.MySqlClient.MySqlCommand();
                        mySqlCommand2.CommandText = "UPDATE mail_queue SET claimed=0  WHERE queue_id=@queue_id"; //reset status
                        mySqlCommand2.Connection = connection;
                        mySqlCommand2.Parameters.AddWithValue("@queue_id", eventRow.queue_id);
                        await mySqlCommand2.ExecuteNonQueryAsync();
                    }
                }
                queueList.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occured in email queue, error:" + ex.Message, ex);
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
        await Task.Delay(1000); //1 sec
    }
}
catch (Exception ex)
{
    Console.WriteLine("General error occured in email queue, error:" + ex.Message, ex);
}
