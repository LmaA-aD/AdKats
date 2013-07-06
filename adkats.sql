-- AdKats Database Setup Script by ColColonCleaner

-- This is run automatically if AdKats senses the database is not set up properly.
-- If you don't want the plugin changing tables/views in your database, you must run this beforehand.

-- Scheduling is needed for update events
SET GLOBAL event_scheduler = ON;

CREATE TABLE IF NOT EXISTS `adkats_accesslist` ( 
	`player_name` VARCHAR(20) NOT NULL, 
	`member_id` INT(11) UNSIGNED NOT NULL DEFAULT 0, 
	`player_email` VARCHAR(254) NOT NULL DEFAULT "test@gmail.com", 
	`access_level` INT(11) NOT NULL DEFAULT 6, 
	PRIMARY KEY (`player_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Access List';

CREATE TABLE IF NOT EXISTS `adkats_records` (
	`record_id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT, 
	`server_id` SMALLINT(5) UNSIGNED NOT NULL, 
	`command_type` VARCHAR(45) NOT NULL DEFAULT "DefaultCommand", 
	`command_action` VARCHAR(45) NOT NULL DEFAULT "DefaultAction", 
	`command_numeric` INT(11) NOT NULL DEFAULT 0, 
	`target_name` VARCHAR(45) NOT NULL DEFAULT "NoTarget", 
	`target_id` INT(11) UNSIGNED DEFAULT NULL, 
	`source_name` VARCHAR(45) NOT NULL DEFAULT "NoNameAdmin", 
	`record_message` VARCHAR(100) NOT NULL DEFAULT "NoMessage", 
	`record_time` TIMESTAMP DEFAULT CURRENT_TIMESTAMP, 
	`adkats_read` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
	`adkats_web` BOOL NOT NULL DEFAULT 0,
	PRIMARY KEY (`record_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Records';
-- ALTER TABLE `adkats_records` ADD 
-- 	CONSTRAINT `adkats_records_fk_server_id` 
-- 		FOREIGN KEY (`server_id`) 
-- 		REFERENCES tbl_server(ServerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;
-- ALTER TABLE `adkats_records` ADD 
-- 	CONSTRAINT `adkats_records_fk_target_id` 
-- 		FOREIGN KEY (`target_id`) 
-- 		REFERENCES tbl_playerdata(PlayerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;

CREATE TABLE IF NOT EXISTS `adkats_serverPlayerPoints` (
	`player_id` INT(11) UNSIGNED NOT NULL, 
	`server_id` SMALLINT(5) UNSIGNED NOT NULL, 
	`punish_points` INT(11) NOT NULL, 
	`forgive_points` INT(11) NOT NULL, 
	`total_points` INT(11) NOT NULL, 
	PRIMARY KEY (`player_id`, `server_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Server Specific Player Points';
-- ALTER TABLE `adkats_serverPlayerPoints` ADD 
-- 	CONSTRAINT `adkats_serverPlayerPoints_fk_player_id` 
-- 		FOREIGN KEY (`player_id`) 
-- 		REFERENCES tbl_playerdata(PlayerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;
-- ALTER TABLE `adkats_serverPlayerPoints` ADD 
-- 	CONSTRAINT `adkats_serverPlayerPoints_fk_server_id` 
-- 		FOREIGN KEY (`server_id`) 
-- 		REFERENCES tbl_server(ServerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;

CREATE TABLE IF NOT EXISTS `adkats_globalPlayerPoints` (
	`player_id` INT(11) UNSIGNED NOT NULL, 
	`punish_points` INT(11) NOT NULL, 
	`forgive_points` INT(11) NOT NULL, 
	`total_points` INT(11) NOT NULL, 
	PRIMARY KEY (`player_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Global Player Points';
-- ALTER TABLE `adkats_globalPlayerPoints` ADD 
-- 	CONSTRAINT `adkats_globalPlayerPoints_fk_player_id` 
-- 		FOREIGN KEY (`player_id`) 
-- 		REFERENCES tbl_playerdata(PlayerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;

CREATE TABLE IF NOT EXISTS `adkats_banlist` ( 
	`ban_id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT, 
	`player_id` INT(11) UNSIGNED NOT NULL, 
	`latest_record_id` INT(11) UNSIGNED NOT NULL, 
	`ban_reason` VARCHAR(100) NOT NULL DEFAULT 'NoReason', 
	`ban_notes` VARCHAR(150) NOT NULL DEFAULT 'NoNotes', 
	`ban_status` enum('Active', 'Expired', 'Disabled') NOT NULL DEFAULT 'Active',
	`ban_startTime` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, 
	`ban_endTime` DATETIME NOT NULL, 
	`ban_enforceName` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
	`ban_enforceGUID` ENUM('Y', 'N') NOT NULL DEFAULT 'Y', 
	`ban_enforceIP` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
	`ban_sync` VARCHAR(100) NOT NULL DEFAULT "-sync-", 
	PRIMARY KEY (`ban_id`), 
	UNIQUE KEY `player_id_UNIQUE` (`player_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Ban Enforcer List';
-- ALTER TABLE `adkats_banlist` ADD 
-- 	CONSTRAINT `adkats_banlist_fk_player_id` 
-- 		FOREIGN KEY (`player_id`) 
-- 		REFERENCES tbl_playerdata(PlayerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;
-- ALTER TABLE `adkats_banlist` ADD 
-- 	CONSTRAINT `adkats_banlist_fk_latest_record_id` 
-- 		FOREIGN KEY (`latest_record_id`) 
-- 		REFERENCES adkats_records(record_id) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;

CREATE TABLE IF NOT EXISTS `adkats_settings` ( 
	`server_id` SMALLINT(5) UNSIGNED NOT NULL, 
	`setting_name` VARCHAR(200) NOT NULL DEFAULT "SettingName", 
	`setting_type` VARCHAR(45) NOT NULL DEFAULT "SettingType", 
	`setting_value` VARCHAR(500) NOT NULL DEFAULT "SettingValue", 
	PRIMARY KEY (`server_id`, `setting_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Setting Sync';
-- ALTER TABLE `adkats_settings` ADD 
-- 	CONSTRAINT `adkats_settings_fk_server_id` 
-- 		FOREIGN KEY (`server_id`) 
-- 		REFERENCES tbl_server(ServerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;

DROP FUNCTION IF EXISTS confirm_logger;
DROP PROCEDURE IF EXISTS import_records;
DROP PROCEDURE IF EXISTS import_ban_manager_bans;
DROP EVENT IF EXISTS ban_status_update;

-- Confirms the existence of server tables/records by XpKiller's Stat Logger, a dependancy of AdKats.
delimiter |
CREATE FUNCTION confirm_logger()
	RETURNS VARCHAR(100) 
	READS SQL DATA 
	BEGIN
		DECLARE response VARCHAR(100);
		SET response = 'OK.';
		IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='tbl_server' AND column_name='ServerID') THEN 
			IF NOT EXISTS (SELECT * FROM `tbl_server`) THEN 
				SET response = 'ERROR. Tables Empty.';
			END IF;
		ELSE
			SET response = 'ERROR. Tables not created.';
		END IF;
		RETURN response;
	END;
|

-- Imports any records from AdKats 2.5.1+ into AdKats 3.0.0
-- Assumes all needed tables are already in the database
CREATE PROCEDURE import_records()
	BEGIN
		DECLARE done INT DEFAULT FALSE;
		-- Create needed variables for imported record
		DECLARE record_id INT(11);
		DECLARE server_id INT(11);
		DECLARE server_ip VARCHAR(45);
		DECLARE command_type VARCHAR(45);
		DECLARE command_action VARCHAR(45);
		DECLARE record_durationMinutes INT(11);
		DECLARE target_guid VARCHAR(100);
		DECLARE target_name VARCHAR(45);
		DECLARE source_name VARCHAR(45);
		DECLARE record_time TIMESTAMP;
		DECLARE adkats_read ENUM('Y', 'N');
		-- Create needed variables for new record
		DECLARE player_id INT(11);
		DECLARE new_server_id INT(11);
		DECLARE old_records CURSOR FOR 
			SELECT 
				`record_id`, 
				`server_id`, 
				`server_ip`, 
				`command_type`, 
				`command_action`, 
				`record_durationMinutes`, 
				`target_guid`, 
				`target_name`, 
				`source_name`, 
				`record_message`, 
				`record_time`, 
				`adkats_read` 
			FROM 
				`adkat_records`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
		SET player_id = -1;
		SET new_server_id = -1;

		IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'adkats_records' AND column_name = 'server_ip') THEN 
			-- Open the reader
			OPEN old_records;
			
			-- Enter the read loop for old records
			read_loop: LOOP
				-- Fetche the first/next record from the cursor
				FETCH old_records INTO 
					record_id, 
					server_id, 
					server_ip, 
					command_type, 
					command_action, 
					record_durationMinutes, 
					target_guid, 
					target_name, 
					source_name, 
					record_time, 
					adkats_read;
				-- Check for done condition
				IF done THEN
				  LEAVE read_loop;
				END IF;
				IF(POSITION('A_' in target_guid) > 0) THEN
					-- Attempt to fetch player ID from tbl_playerdata
					SELECT `PlayerID` INTO player_id FROM `tbl_playerdata` WHERE `EAGUID` = target_guid;
					IF (player_id < 0) THEN
						-- If ID not found, insert the new player
						INSERT INTO `tbl_playerdata` (`SoldierName`, `EAGUID`) VALUES (target_name, target_guid);
						SET player_id = LAST_INSERT_ID();
					END IF;
				ELSE
					SET player_id = NULL;
				END IF;
				-- Attempt to fetch correct server ID from tbl_server
				SELECT `ServerID` INTO new_server_id FROM `tbl_server` WHERE `IP_Address` = server_ip;
				IF (new_server_id < 0) THEN
					-- If ID not found, insert new server
					INSERT INTO `tbl_server` (`IP_Address`) VALUES (server_ip);
					SET new_server_id = LAST_INSERT_ID();
				END IF;
				
				-- Fix an error injected in 2.5.1
				IF (command_type = 'Punish' AND record_durationMinutes > 0) THEN 
					SET command_action = 'TempBan';
				END IF;
				
				-- Insert the new record
				INSERT INTO `adkats_records` 
				(
					`server_id`, 
					`command_type`, 
					`command_action`, 
					`command_numeric`, 
					`target_name`, 
					`target_id`, 
					`source_name`, 
					`record_message`, 
					`record_time`, 
					`adkats_read`
				)
				VALUES 
				(
					new_server_id, 
					command_type, 
					command_action, 
					record_durationMinutes, 
					target_name, 
					player_id, 
					record_message, 
					record_time, 
					adkats_read
				);
			END LOOP;
			
			-- Close the reader
			CLOSE old_records;
		END IF;
	END;
|

-- Imports any records from the "Ban Manager" by DaMagicWobber into AdKats Ban Manager
CREATE PROCEDURE import_ban_manager_bans()
BEGIN
	DECLARE done INT DEFAULT FALSE;
	-- Create all variables for imported ban
	DECLARE server_ip VARCHAR(45);
	DECLARE target_name VARCHAR(45);
	DECLARE target_guid VARCHAR(100);
	DECLARE ban_reason VARCHAR(100);
	DECLARE source_name VARCHAR(45);
	DECLARE ban_duration DATETIME;
	DECLARE ban_time TIMESTAMP;
	-- Create needed variables for new record
	DECLARE player_id INT(11);
	DECLARE server_id INT(11);
	DECLARE ban_manager_bans CURSOR FOR 
	SELECT 
		`bm_banlist`.`banID` AS `ban_id`, 
		`bm_servergroup`.`serverip` AS `server_ip`, 
		`bm_soldiers`.`soldiername` AS `target_name`, 
		`bm_soldiers`.`eaguid` AS `target_guid`, 
		`bm_banlist`.`ban_reason` AS `ban_reason`, 
		`bm_banlist`.`ban_admin` AS `source_name`, 
		`bm_banlist`.`ban_duration` AS `ban_duration`, 
		`bm_banlist`.`timestamp` AS `ban_time`
	FROM 
		`bm_banlist` 
	INNER JOIN 
		`bm_soldiers` ON `bm_banlist`.`soldierID` = `bm_soldiers`.`soldierID` 
	INNER JOIN 
		`bm_servergroup` ON `bm_banlist`.`servergroup` = `bm_servergroup`.`servergroup`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
	SET player_id = -1;
	SET server_id = -1;

	-- If the bm_banlist table exists then that ban manager was in use
	IF (EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='bm_banlist')) THEN 
		
		-- Open the reader
		OPEN ban_manager_bans;
		
		-- Enter the read loop for old records
		read_loop: LOOP
			-- Fetche the first/next record from the cursor
			FETCH ban_manager_bans INTO 
				server_ip, 
				target_name, 
				target_guid, 
				ban_reason, 
				source_name, 
				ban_duration, 
				ban_time;
			-- Check for done condition
			IF done THEN
			  LEAVE read_loop;
			END IF;
			
			-- Attempt to fetch player ID from tbl_playerdata
			SELECT `PlayerID` INTO player_id FROM `tbl_playerdata` WHERE `EAGUID` = target_guid;
			IF (player_id < 0) THEN
				-- If ID not found, insert the new player
				INSERT INTO `tbl_playerdata` (`SoldierName`, `EAGUID`) VALUES (target_name, target_guid);
				SET player_id = LAST_INSERT_ID();
			END IF;
			
			-- Attempt to fetch correct server ID from tbl_server
			SELECT `ServerID` INTO server_id FROM `tbl_server` WHERE `IP_Address` = server_ip;
			IF (server_id < 0) THEN
				-- If ID not found, insert new server
				INSERT INTO `tbl_server` (`IP_Address`) VALUES (server_ip);
				SET server_id = LAST_INSERT_ID();
			END IF;
			
			-- Insert the new record
			INSERT INTO `adkats_records` 
			(
				`server_id`, 
				`command_type`, 
				`command_action`, 
				`command_numeric`, 
				`target_name`, 
				`target_id`, 
				`source_name`, 
				`record_message`, 
				`record_time`, 
				`adkats_read`
			)
			VALUES 
			(
				server_id, 
				'TempBan', 
				'TempBan', 
				record_durationMinutes, 
				target_name, 
				player_id, 
				source_name, 
				record_message, 
				record_time, 
				adkats_read
			);

			-- Insert the new ban
			INSERT INTO `adkats_banlist` 
			(
				`record_id`, 
				`ban_reason`, 
				`ban_status`, 
				`ban_startTime`, 
				`ban_endTime`, 
				`ban_enforceName`, 
				`ban_enforceGUID`, 
				`ban_enforceIP`, 
				`ban_sync`
			)
			VALUES 
			(
				record_id, 
				record_message, 
				'Active', 
				record_time, 
				DATE_ADD(record_time, INTERVAL record_durationMinutes MINUTE), 
				'N', 
				'Y', 
				'N',
				'-sync-'
			);
		END LOOP;
		
		-- Close the reader
		CLOSE ban_manager_bans;

	END IF;
END;
|

CREATE EVENT ban_status_update
	ON SCHEDULE EVERY 5 MINUTE 
	COMMENT 'Updates expired bans for sync every 5 minutes.' 
	DO 
	BEGIN
		UPDATE 
			`adkats_banlist` 
		SET 
			`ban_status` = 'Expired', 
			`ban_sync` = '-sync-' 
		WHERE 
			`ban_endTime` < NOW();
	END;

-- Updates player points when punishments or forgivness logs are added in the record table
CREATE TRIGGER adkats_update_point_insert BEFORE INSERT ON `adkats_records`
	FOR EACH ROW 
	BEGIN 
		DECLARE command_type VARCHAR(45);
		DECLARE server_id INT(11);
		DECLARE player_id INT(11);
		SET command_type = NEW.command_type;
		SET server_id = NEW.server_id;
		SET player_id = NEW.target_id;

		IF(command_type = 'Punish') THEN
			INSERT INTO `adkats_serverPlayerPoints` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 1, 0, 1) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` + 1, 
				`total_points` = `total_points` + 1;
			INSERT INTO `adkats_globalPlayerPoints` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 1, 0, 1) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` + 1, 
				`total_points` = `total_points` + 1;
		ELSEIF (command_type = 'Forgive') THEN
			INSERT INTO `adkats_serverPlayerPoints` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 0, 1, -1) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` + 1, 
				`total_points` = `total_points` - 1;
			INSERT INTO `adkats_globalPlayerPoints` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 0, 1, -1) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` + 1, 
				`total_points` = `total_points` - 1;
		END IF;
	END;
|

-- Updates player points when punishments or forgivness logs are removed from the record table
CREATE TRIGGER adkats_update_point_delete AFTER DELETE ON `adkats_records`
	FOR EACH ROW 
	BEGIN 
		DECLARE command_type VARCHAR(45);
		DECLARE server_id INT(11);
		DECLARE player_id INT(11);
		SET command_type = OLD.command_type;
		SET server_id = OLD.server_id;
		SET player_id = OLD.target_id;

		IF(command_type = 'Punish') THEN
			INSERT INTO `adkats_serverPlayerPoints` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` - 1, 
				`total_points` = `total_points` - 1;
			INSERT INTO `adkats_globalPlayerPoints` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` - 1, 
				`total_points` = `total_points` - 1;
		ELSEIF (command_type = 'Forgive') THEN
			INSERT INTO `adkats_serverPlayerPoints` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` - 1, 
				`total_points` = `total_points` + 1;
			INSERT INTO `adkats_globalPlayerPoints` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` - 1, 
				`total_points` = `total_points` + 1;
		END IF;
	END;
|
delimiter ;

CREATE OR REPLACE VIEW `adkats_totalcmdissued` AS
SELECT
  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Move' OR adkats_records.command_type = 'ForceMove') AS 'Moves',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Teamswap') AS 'TeamSwaps',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Kill') AS 'Kills',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Kick') AS 'Kicks',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'TempBan' OR adkats_records.command_action = 'TempBan') AS 'TempBans',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'PermaBan' OR adkats_records.command_action = 'PermaBan') AS 'PermaBans',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Punish') AS 'Punishes',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Forgive') AS 'Forgives',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Report' OR adkats_records.command_type = 'CallAdmin') AS 'Reports',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'ConfirmReport') AS 'UsedReports',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'AdminSay') AS 'AdminSays',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'PlayerSay') AS 'PlayerSays',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'AdminYell') AS 'AdminYells',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'PlayerYell') AS 'PlayerYells',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Mute') AS 'PlayerMutes',

  (SELECT COUNT(*)
   FROM adkats_records) AS 'TotalCommands';
