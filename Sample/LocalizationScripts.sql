DECLARE @i int, @total int, @groupId int;
SELECT ROW_NUMBER() OVER(ORDER BY wslocalizationgroupid) AS rowNum, WSLocalizationGroupId INTO #groupids FROM WSLocalizationGroupModules WHERE Module = 0
SELECT @i = 1, @total = COUNT(*) FROM #groupids

WHILE @i <= @total
BEGIN
	SELECT @groupId = WSLocalizationGroupid FROM #groupids WHERE rowNum = @i;

	EXEC GTSAddWSLocalizationRow @GroupID, 'CheckoutPage/Index', 'PaymentATypeTitle', 'Payment Type Title', 0, 0 
    EXEC GTSAddWSLocalizationRow @GroupID, 'CheckoutPage/Index', 'PaymentATypeDescription', 'Payment Type Description', 0, 0 

	/*
	The below line is important because the webstore doesn’t display the strings if the translationLanguageId
	is set to null, so this sets the fields to 0 if they are null, which only happens when these updates are
	made directly against the webstore DB vs against the Galaxy DB and then published to the webstore DB.
	*/
	UPDATE WSLocalization SET TranslationLanguageID = isnull(translationLanguageid, 0)

	SELECT @i = @i + 1
END

DROP TABLE #groupids