-- AdKats Database Setup Script by ColColonCleaner

-- This is run automatically if AdKats senses the database is not set up properly.
-- If you don't want the plugin changing tables/views in your database, you must run this beforehand.

-- If the tables needed are not in the database yet, the below two queries will be success
CREATE TABLE IF NOT EXISTS `adkat_records` ( 
       `record_id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT, 
       `server_id` INT(11) UNSIGNED NOT NULL, 
       `command_type` VARCHAR(45) NOT NULL DEFAULT "DefaultCommand", 
       `command_action` VARCHAR(45) NOT NULL DEFAULT "DefaultAction", 
       `player_id` INT(11) UNSIGNED NOT NULL, 
       `source_name` VARCHAR(45) NOT NULL DEFAULT "NoNameAdmin", 
       `record_message` VARCHAR(100) NOT NULL DEFAULT "NoMessage", 
       `record_time` TIMESTAMP DEFAULT CURRENT_TIMESTAMP, 
       `adkats_read` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
       PRIMARY KEY (`record_id`), 
       CONSTRAINT `fk_server_id` 
              FOREIGN KEY (`server_id` ) 
              REFERENCES `tbl_server`.`ServerID` 
              ON DELETE CASCADE 
              ON UPDATE NO ACTION, 
       CONSTRAINT `fk_player_id` 
              FOREIGN KEY (`player_id` ) 
              REFERENCES `tbl_playerdata`.`PlayerID` 
              ON DELETE CASCADE 
              ON UPDATE NO ACTION
);

CREATE TABLE IF NOT EXISTS `adkat_accesslist` ( 
       `player_name` VARCHAR(45) NOT NULL DEFAULT "NoPlayer", 
       `player_guid` VARCHAR(100) NOT NULL DEFAULT 'WAITING ON USE FOR GUID', 
       `access_level` INT(11) NOT NULL DEFAULT 6, 
       PRIMARY KEY (`player_name`), UNIQUE KEY `player_name_UNIQUE` (`player_name`)
);

CREATE TABLE IF NOT EXISTS `adkat_banlist` ( 
       `ban_id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT, 
       `record_id` INT(11) UNSIGNED NOT NULL, 
       `player_id` INT(11) UNSIGNED NOT NULL, 
       `server_id` INT(11) UNSIGNED NOT NULL, 
       `ban_status` enum('Enabled', 'Disabled') NOT NULL DEFAULT 'Enabled',
       `ban_reason` VARCHAR(100) NOT NULL DEFAULT 'NoReason', 
       `ban_notes` VARCHAR(150) NOT NULL DEFAULT 'NoNotes', 
       `ban_sync` VARCHAR(100) NOT NULL DEFAULT "-sync-", 
       `ban_startTime` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, 
       `ban_endTime` DATETIME NOT NULL, 
       `ban_displayDurationMinutes` int(11) NOT NULL DEFAULT 0, 
       PRIMARY KEY (`ban_id`), 
       UNIQUE KEY `ban_id_UNIQUE` (`ban_id`), 
       UNIQUE KEY `player_id_UNIQUE` (`player_id`), 
       CONSTRAINT `fk_player_id` 
              FOREIGN KEY (`player_id` ) 
              REFERENCES `tbl_playerdata`.`PlayerID` 
              ON DELETE CASCADE 
              ON UPDATE NO ACTION, 
       CONSTRAINT `fk_server_id` 
              FOREIGN KEY (`server_id` ) 
              REFERENCES `tbl_server`.`ServerID` 
              ON DELETE CASCADE 
              ON UPDATE NO ACTION, 
       CONSTRAINT `fk_record_id` 
              FOREIGN KEY (`record_id` ) 
              REFERENCES `adkat_records`.`record_id` 
              ON DELETE CASCADE 
              ON UPDATE NO ACTION
);

CREATE OR REPLACE VIEW `adkat_playerlist` AS
SELECT `adkat_records`.`target_name` AS `player_name`,
       `adkat_records`.`target_guid` AS `player_guid`,
       `adkat_records`.`server_id` AS `server_id`
FROM `adkat_records`
GROUP BY `adkat_records`.`target_guid`,
         `adkat_records`.`server_id`
ORDER BY `adkat_records`.`target_name`;

CREATE OR REPLACE VIEW `adkat_playerpoints` AS
SELECT
  `adkat_playerlist`.`player_name` AS `playername`,
  `adkat_playerlist`.`player_guid` AS `playerguid`,
  `adkat_playerlist`.`server_id` AS `serverid`,
  ((SELECT
    COUNT(`adkat_records`.`target_guid`)
  FROM `adkat_records`
  WHERE ((`adkat_records`.`command_type` = 'Punish') AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`) AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`))) - (SELECT
    COUNT(`adkat_records`.`target_guid`)
  FROM `adkat_records`
  WHERE ((`adkat_records`.`command_type` = 'Forgive') AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`) AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)))) AS `totalpoints`
FROM `adkat_playerlist` ORDER BY `totalpoints` DESC;

CREATE OR REPLACE VIEW `adkat_weeklyplayerpoints` AS
SELECT 
`adkat_playerlist`.`player_name` AS `playername`, 
`adkat_playerlist`.`player_guid` AS `playerguid`, 
`adkat_playerlist`.`server_id` AS `serverid`, 
  ( (SELECT count(`adkat_records`.`target_guid`)
  FROM `adkat_records`
  WHERE (    (`adkat_records`.`command_type` = 'Punish')
               AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
               AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)
      AND (`adkat_records`.`record_time` between date_sub(now(),INTERVAL 7 DAY) and now()))) -
 (SELECT count(`adkat_records`.`target_guid`)
  FROM `adkat_records`
  WHERE (    (`adkat_records`.`command_type` = 'Forgive')
               AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
      	  AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)
      AND (`adkat_records`.`record_time` between date_sub(now(),INTERVAL 7 DAY) and now())))
   ) AS `totalpoints`
FROM `adkat_playerlist` ORDER BY `totalpoints` DESC;

CREATE OR REPLACE VIEW `adkat_reports` AS
SELECT `adkat_records`.`record_id` AS `record_id`,
       `adkat_records`.`server_id` AS `server_id`,
       `adkat_records`.`command_type` AS `command_type`,
       `adkat_records`.`record_durationMinutes` AS `record_durationMinutes`,
       `adkat_records`.`target_guid` AS `target_guid`,
       `adkat_records`.`target_name` AS `target_name`,
       `adkat_records`.`source_name` AS `source_name`,
       `adkat_records`.`record_message` AS `record_message`,
       `adkat_records`.`record_time` AS `record_time`
FROM `adkat_records`
WHERE ((`adkat_records`.`command_type` = 'Report')
       OR (`adkat_records`.`command_type` = 'CallAdmin'));

CREATE OR REPLACE VIEW `adkat_naughtylist` AS
SELECT `adkat_playerpoints`.`serverid` AS `server_id`,
       `adkat_playerpoints`.`playername` AS `player_name`,
       `adkat_playerpoints`.`totalpoints` AS `total_points`
FROM `adkat_playerpoints`
WHERE (`adkat_playerpoints`.`totalpoints` > 0)
ORDER BY  `adkat_playerpoints`.`totalpoints` DESC,
          `adkat_playerpoints`.`serverid`,
          `adkat_playerpoints`.`playername`;

CREATE OR REPLACE VIEW `adkat_weeklynaughtylist` AS
SELECT `adkat_weeklyplayerpoints`.`serverid` AS `server_id`,
       `adkat_weeklyplayerpoints`.`playername` AS `player_name`,
       `adkat_weeklyplayerpoints`.`totalpoints` AS `total_points`
FROM `adkat_weeklyplayerpoints`
WHERE (`adkat_weeklyplayerpoints`.`totalpoints` > 0)
ORDER BY `adkat_weeklyplayerpoints`.`totalpoints` DESC,
         `adkat_weeklyplayerpoints`.`serverid`,
         `adkat_weeklyplayerpoints`.`playername`;

CREATE OR REPLACE VIEW `adkat_totalcmdissued` AS
SELECT
  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Move' OR adkat_records.command_type = 'ForceMove') AS 'Moves',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Teamswap') AS 'TeamSwaps',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Kill') AS 'Kills',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Kick') AS 'Kicks',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'TempBan' OR adkat_records.command_action = 'TempBan') AS 'TempBans',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'PermaBan' OR adkat_records.command_action = 'PermaBan') AS 'PermaBans',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Punish') AS 'Punishes',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Forgive') AS 'Forgives',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Report' OR adkat_records.command_type = 'CallAdmin') AS 'Reports',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'ConfirmReport') AS 'UsedReports',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'AdminSay') AS 'AdminSays',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'PlayerSay') AS 'PlayerSays',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'AdminYell') AS 'AdminYells',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'PlayerYell') AS 'PlayerYells',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Mute') AS 'PlayerMutes',

  (SELECT COUNT(*)
   FROM adkat_records) AS 'TotalCommands';

-- Run these last, if they fail it means it was a new DB, if it succeeds their previous data was saved.
ALTER TABLE `adkat_records` MODIFY `record_time` TIMESTAMP DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `adkat_records` MODIFY `server_id` int(11) NOT NULL DEFAULT -1;
ALTER TABLE `adkat_records` MODIFY `command_type` varchar(45) NOT NULL DEFAULT "DefaultCommand"; 
ALTER TABLE `adkat_records` MODIFY `record_durationMinutes` int(11) NOT NULL DEFAULT 0; 
ALTER TABLE `adkat_records` MODIFY `target_guid` varchar(100) NOT NULL DEFAULT "EA_NoGUID"; 
ALTER TABLE `adkat_records` MODIFY `target_name` varchar(45) NOT NULL DEFAULT "NoTarget"; 
ALTER TABLE `adkat_records` MODIFY `source_name` varchar(45) NOT NULL DEFAULT "NoNameAdmin"; 
ALTER TABLE `adkat_records` MODIFY `record_message` varchar(100) NOT NULL DEFAULT "NoMessage"; 
ALTER TABLE `adkat_records` MODIFY `adkats_read` ENUM('Y', 'N') NOT NULL DEFAULT 'N';

DROP TABLE IF EXISTS `adkat_teamswapwhitelist`;
