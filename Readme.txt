Windows backup script with file verification

	The script uses "robocopy.exe" and "fc.exe".

	The program reads from the following files:
		destination.txt
		options.txt
		robocopy-arguments.txt
		sources.txt
	These files are located at /cfg/ in the repository.

	destination.txt:
		Path to the target folder/drive of the backup. One line.
		
	sources.txt:
		Path to folders and/or files to backup. One item per line.
		
	options.txt:
		Configuration options. See the file itself for details.
		
	robocopy-arguments.txt
		Arguments to pass to robocopy.exe other than the file paths.