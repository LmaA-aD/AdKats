DELIMITER //

DROP PROCEDURE IF EXISTS addLogPlayerID //
CREATE PROCEDURE addLogPlayerID()
BEGIN

-- add logPlayerID column safely
IF NOT EXISTS( (SELECT * FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
        AND COLUMN_NAME='logPlayerID' AND TABLE_NAME='tbl_chatlog') ) THEN
        ALTER TABLE `tbl_chatlog` ADD COLUMN `logPlayerID` INT(10) UNSIGNED DEFAULT NULL AFTER `logSubset`;
  	ALTER TABLE `tbl_chatlog` ADD INDEX (`logPlayerID`);
  	ALTER TABLE `tbl_chatlog` ADD CONSTRAINT `tbl_chatlog_ibfk_player_id` FOREIGN KEY (`logPlayerID`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE;
  	UPDATE 
  		`tbl_chatlog`
  	INNER JOIN 
  		`tbl_playerdata`
  	ON 
  		`tbl_chatlog`.`logSoldierName` = `tbl_playerdata`.`SoldierName` 
  	SET 
  		`tbl_chatlog`.`logPlayerID` = `tbl_playerdata`.`PlayerID`
  	WHERE 
  		`tbl_playerdata`.`SoldierName` <> 'AutoAdmin' 
  	AND 
  		`tbl_playerdata`.`SoldierName` <> 'AdKats' 
  	AND 
  		`tbl_playerdata`.`SoldierName` <> 'Server' 
  	AND 
  		`tbl_playerdata`.`SoldierName` <> 'BanEnforcer'
  	AND 
  		`tbl_chatlog`.`logPlayerID` IS NULL;
END IF;

END //

CALL addLogPlayerID() //

DELIMITER ;
