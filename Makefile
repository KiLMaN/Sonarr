XBUILD=xbuild
CONF=Release
dirOut="sonarr.`date +"%Y-%m-%d"`"

all : git clean buildSources buildUI 

install: all 
	@cp -r _output/ ../$(dirOut) 	
	@rm ../current -f
	@cd ../ && ln -s $(dirOut) current

buildSources:
	@echo "Building Sources"
	@$(XBUILD) /p:Configuration=$(CONF) src/NzbDrone.sln

buildUI:
	@echo "Updating NPM"
	@npm install
	@echo "Building UI"
	@gulp build

git:
	@echo "Getting last sources from GIT"
	@git pull

clean:
	@echo "Removing _output direcotry"
	@rm -rf _output
